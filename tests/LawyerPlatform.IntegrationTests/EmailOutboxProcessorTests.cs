using BuildingBlock.Application.Email;
using BuildingBlock.Application.Time;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Infrastructure.Email;
using LawyerPlatform.Infrastructure.Options;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Exceptions;

namespace LawyerPlatform.IntegrationTests;

public sealed class EmailOutboxProcessorTests
{
    [Fact]
    public async Task SuccessfulSend_MarksSent_AndNeverSendsAgain()
    {
        await using var fixture = await OutboxFixture.CreateAsync();
        var sender = new FakeEmailSender();
        await fixture.QueueAsync("success", TestContext.Current.CancellationToken);

        var first = await fixture.CreateProcessor(sender).ProcessBatchAsync(TestContext.Current.CancellationToken);
        var second = await fixture.CreateProcessor(sender).ProcessBatchAsync(TestContext.Current.CancellationToken);
        var message = await fixture.LoadSingleAsync();

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(1, sender.SendCount);
        Assert.Equal(EmailOutboxStatus.Sent, message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.NotNull(message.ProcessedOnUtc);
        Assert.Null(message.LastError);
    }

    [Fact]
    public async Task TransientFailure_RetriesWhenDue_AndStopsAtMaximumAttempts()
    {
        await using var fixture = await OutboxFixture.CreateAsync(maxAttempts: 2);
        var sender = new FakeEmailSender { Fail = true };
        await fixture.QueueAsync("retry", TestContext.Current.CancellationToken);

        Assert.Equal(1, await fixture.CreateProcessor(sender).ProcessBatchAsync(TestContext.Current.CancellationToken));
        var afterFirst = await fixture.LoadSingleAsync();
        Assert.Equal(EmailOutboxStatus.Pending, afterFirst.Status);
        Assert.Equal(1, afterFirst.AttemptCount);
        Assert.NotNull(afterFirst.NextAttemptOnUtc);
        Assert.Equal("Email.SendFailed", afterFirst.LastError);

        Assert.Equal(0, await fixture.CreateProcessor(sender).ProcessBatchAsync(TestContext.Current.CancellationToken));
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(2);
        Assert.Equal(1, await fixture.CreateProcessor(sender).ProcessBatchAsync(TestContext.Current.CancellationToken));

        var exhausted = await fixture.LoadSingleAsync();
        Assert.Equal(EmailOutboxStatus.Failed, exhausted.Status);
        Assert.Equal(2, exhausted.AttemptCount);
        Assert.Null(exhausted.NextAttemptOnUtc);
        Assert.Equal(2, sender.SendCount);
        Assert.Equal(0, await fixture.CreateProcessor(sender).ProcessBatchAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Queue_DeduplicatesLogicalMessage_AndProcessorHonorsBatchSizeAndCancellation()
    {
        await using var fixture = await OutboxFixture.CreateAsync(batchSize: 2);
        await fixture.QueueAsync("duplicate", TestContext.Current.CancellationToken);
        await fixture.QueueAsync("duplicate", TestContext.Current.CancellationToken);
        await fixture.QueueAsync("second", TestContext.Current.CancellationToken);
        await fixture.QueueAsync("third", TestContext.Current.CancellationToken);

        Assert.Equal(3, await fixture.CountAsync());

        var sender = new FakeEmailSender();
        Assert.Equal(2, await fixture.CreateProcessor(sender).ProcessBatchAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, sender.SendCount);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.CreateProcessor(sender).ProcessBatchAsync(cancellation.Token));
    }

    [Fact]
    public async Task PermanentFailure_MarksMessageFailedWithoutRetry()
    {
        await using var fixture = await OutboxFixture.CreateAsync();
        var sender = new FakeEmailSender
        {
            Exception = new EmailServiceException(
                Error.Validation("Email.InvalidMessage", "invalid", source: "Email"))
        };
        await fixture.QueueAsync("permanent", TestContext.Current.CancellationToken);

        Assert.Equal(1, await fixture.CreateProcessor(sender).ProcessBatchAsync(TestContext.Current.CancellationToken));
        var message = await fixture.LoadSingleAsync();

        Assert.Equal(EmailOutboxStatus.Failed, message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.Null(message.NextAttemptOnUtc);
        Assert.Equal("Email.InvalidMessage", message.LastError);
    }

    private sealed class OutboxFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<LawyerPlatformDbContext> _dbOptions;
        private readonly EmailOutboxOptions _options;

        private OutboxFixture(
            SqliteConnection connection,
            DbContextOptions<LawyerPlatformDbContext> dbOptions,
            EmailOutboxOptions options,
            MutableClock clock)
        {
            _connection = connection;
            _dbOptions = dbOptions;
            _options = options;
            Clock = clock;
        }

        public MutableClock Clock { get; }

        public static async Task<OutboxFixture> CreateAsync(int batchSize = 20, int maxAttempts = 5)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
                .UseSqlite(connection)
                .Options;
            await using (var context = new LawyerPlatformDbContext(options))
            {
                await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            }

            return new OutboxFixture(
                connection,
                options,
                new EmailOutboxOptions
                {
                    Enabled = true,
                    BatchSize = batchSize,
                    MaxAttempts = maxAttempts,
                    PollingIntervalSeconds = 1,
                    ClaimLeaseSeconds = 300
                },
                new MutableClock(new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc)));
        }

        public async Task QueueAsync(string logicalKey, CancellationToken cancellationToken)
        {
            await using var context = new LawyerPlatformDbContext(_dbOptions);
            var outbox = new EmailNotificationOutbox(
                context,
                Clock,
                NullLogger<EmailNotificationOutbox>.Instance);
            await outbox.QueueAsync(
                new QueueEmailNotification(
                    EmailNotificationType.ClientReactivated,
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    $"ClientReactivated:{logicalKey}",
                    "client@example.com",
                    "Safe subject",
                    "<div dir=\"rtl\" lang=\"ar\">مرحبًا</div><div dir=\"ltr\" lang=\"en\">Hello</div>"),
                cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        public EmailOutboxProcessor CreateProcessor(IEmailSender sender)
        {
            var context = new LawyerPlatformDbContext(_dbOptions);
            return new EmailOutboxProcessor(
                context,
                sender,
                Clock,
                Options.Create(_options),
                NullLogger<EmailOutboxProcessor>.Instance);
        }

        public async Task<EmailOutboxMessage> LoadSingleAsync()
        {
            await using var context = new LawyerPlatformDbContext(_dbOptions);
            return await context.EmailOutboxMessages
                .AsNoTracking()
                .SingleAsync(TestContext.Current.CancellationToken);
        }

        public async Task<int> CountAsync()
        {
            await using var context = new LawyerPlatformDbContext(_dbOptions);
            return await context.EmailOutboxMessages.CountAsync(TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }

    private sealed class MutableClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public bool Fail { get; init; }
        public Exception? Exception { get; init; }
        public int SendCount { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            SendCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            if (Fail)
            {
                throw new IOException("provider detail must not be persisted");
            }

            return Task.CompletedTask;
        }
    }
}
