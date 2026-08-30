using System.Linq.Expressions;
using System.Reflection;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.ContactInquiries;
using LawyerPlatform.Application.Features.ContactInquiries.Create;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ContactInquiries;

namespace LawyerPlatform.UnitTests.ContactInquiries;

public sealed class CreateContactInquiryHandlerTests
{
    private static readonly DateTime CreatedOnUtc =
        new(2026, 8, 30, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ValidCreateStagesInquiryAndExactlyTwoOrderedOutboxNotificationsThenSavesOnce()
    {
        var session = new FakePersistenceSession();
        var unitOfWork = new FakeUnitOfWork(session);
        var outbox = new FakeOutbox(session);
        var coordinator = new EmailNotificationCoordinator(
            new FakeEmailFactory(),
            outbox,
            null!,
            null!);
        var handler = new CreateContactInquiryCommandHandler(
            unitOfWork,
            new FixedClock(CreatedOnUtc),
            new FixedRecipientProvider("Support@avokatoo.com"),
            coordinator);

        var result = await handler.Handle(ValidCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(CreatedOnUtc, result.Value.CreatedOnUtc);
        Assert.Single(session.CommittedInquiries);
        Assert.Equal(result.Value.Id, session.CommittedInquiries[0].Id);
        Assert.Equal(2, session.CommittedNotifications.Count);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);

        var support = session.CommittedNotifications[0];
        var confirmation = session.CommittedNotifications[1];
        Assert.Equal(EmailNotificationType.ContactInquirySupportNotification, support.NotificationType);
        Assert.Equal("Support@avokatoo.com", support.RecipientEmail);
        Assert.Equal($"ContactInquirySupport:{result.Value.Id:N}", support.IdempotencyKey);
        Assert.Equal(EmailNotificationType.ContactInquiryConfirmation, confirmation.NotificationType);
        Assert.Equal("ahmed@example.test", confirmation.RecipientEmail);
        Assert.Equal($"ContactInquiryConfirmation:{result.Value.Id:N}", confirmation.IdempotencyKey);
        Assert.NotEqual(support.IdempotencyKey, confirmation.IdempotencyKey);
    }

    [Fact]
    public async Task FailedSaveDoesNotCommitInquiryOrOutboxNotifications()
    {
        var session = new FakePersistenceSession();
        var unitOfWork = new FakeUnitOfWork(session) { FailSave = true };
        var coordinator = new EmailNotificationCoordinator(
            new FakeEmailFactory(),
            new FakeOutbox(session),
            null!,
            null!);
        var handler = new CreateContactInquiryCommandHandler(
            unitOfWork,
            new FixedClock(CreatedOnUtc),
            new FixedRecipientProvider("Support@avokatoo.com"),
            coordinator);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(ValidCommand(), TestContext.Current.CancellationToken));

        Assert.Empty(session.CommittedInquiries);
        Assert.Empty(session.CommittedNotifications);
        Assert.Single(session.StagedInquiries);
        Assert.Equal(2, session.StagedNotifications.Count);
    }

    [Fact]
    public void HandlerHasNoDirectSmtpDependency()
    {
        var constructorParameters = typeof(CreateContactInquiryCommandHandler)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(constructorParameters, type => typeof(IEmailSender).IsAssignableFrom(type));
    }

    private static CreateContactInquiryCommand ValidCommand()
        => new(
            "Ahmed Mohamed",
            "01012345678",
            "ahmed@example.test",
            "Technical Support",
            "Please contact me.");

    private sealed class FakePersistenceSession
    {
        public List<ContactInquiry> StagedInquiries { get; } = [];
        public List<QueueEmailNotification> StagedNotifications { get; } = [];
        public List<ContactInquiry> CommittedInquiries { get; } = [];
        public List<QueueEmailNotification> CommittedNotifications { get; } = [];
    }

    private sealed class FakeUnitOfWork(FakePersistenceSession session)
        : IUnitOfWork<LawyerPlatformWritePersistence>
    {
        private readonly FakeContactInquiryRepository _repository = new(session);

        public int SaveChangesCalls { get; private set; }
        public bool FailSave { get; init; }

        public IWriteRepository<TEntity, LawyerPlatformWritePersistence> WriteRepository<TEntity>()
            where TEntity : class, IAggregateRoot
        {
            if (typeof(TEntity) != typeof(ContactInquiry))
            {
                throw new InvalidOperationException("Unexpected aggregate type.");
            }

            return (IWriteRepository<TEntity, LawyerPlatformWritePersistence>)(object)_repository;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveChangesCalls++;
            if (FailSave)
            {
                throw new InvalidOperationException("Simulated persistence failure.");
            }

            session.CommittedInquiries.AddRange(session.StagedInquiries);
            session.CommittedNotifications.AddRange(session.StagedNotifications);
            return Task.FromResult(
                session.StagedInquiries.Count + session.StagedNotifications.Count);
        }

        public Task<IApplicationTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeContactInquiryRepository(FakePersistenceSession session)
        : IWriteRepository<ContactInquiry, LawyerPlatformWritePersistence>
    {
        public Task<ContactInquiry?> GetByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull
            => Task.FromResult<ContactInquiry?>(null);

        public Task<ContactInquiry?> GetByPropertyAsync(
            Expression<Func<ContactInquiry, bool>> predicate,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ContactInquiry?>(null);

        public Task<ContactInquiry?> FirstOrDefaultAsync(
            Specification<ContactInquiry> specification,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ContactInquiry?>(null);

        public Task AddAsync(
            ContactInquiry entity,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.StagedInquiries.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(
            IEnumerable<ContactInquiry> entities,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.StagedInquiries.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(ContactInquiry entity) => throw new NotSupportedException();
        public void UpdateRange(IEnumerable<ContactInquiry> entities) => throw new NotSupportedException();
        public void Delete(ContactInquiry entity) => throw new NotSupportedException();
        public void DeleteRange(IEnumerable<ContactInquiry> entities) => throw new NotSupportedException();
    }

    private sealed class FakeOutbox(FakePersistenceSession session)
        : IEmailNotificationOutbox
    {
        public Task QueueAsync(
            QueueEmailNotification notification,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.StagedNotifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmailFactory : IEmailNotificationFactory
    {
        public EmailNotificationContent Create(
            EmailNotificationType notificationType,
            EmailNotificationModel model)
        {
            Assert.IsType<ContactInquiryEmailNotificationModel>(model);
            return new EmailNotificationContent(
                $"subject:{notificationType}",
                $"<p>{notificationType}</p>");
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FixedRecipientProvider(string supportEmail)
        : IContactUsRecipientProvider
    {
        public string SupportEmail { get; } = supportEmail;
    }
}
