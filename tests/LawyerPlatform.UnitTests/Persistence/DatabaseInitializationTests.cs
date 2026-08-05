using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Infrastructure.Options;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Persistence;

public sealed class DatabaseInitializationTests
{
    private const string SafeConnectionString =
        "Server=localhost;Database=LawyerPlatformTests;Integrated Security=True;TrustServerCertificate=True";

    [Fact]
    public async Task SeedingExecutesAfterSuccessfulMigration()
    {
        var operations = new List<string>();
        var migration = new RecordingMigrationService(operations);
        var seeding = new RecordingEnsureSeeding(operations);
        await using var dbContext = CreateDbContext(SafeConnectionString);
        var service = CreateHostedService(dbContext, migration, seeding, applyMigrations: true);

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["compiled", "pending", "migrate", "seed"],
            operations);
    }

    [Fact]
    public async Task SeedingDoesNotExecuteWhenMigrationFailsAndExceptionIsPropagated()
    {
        var operations = new List<string>();
        var expected = new InvalidOperationException("migration failed");
        var migration = new RecordingMigrationService(operations, expected);
        var seeding = new RecordingEnsureSeeding(operations);
        await using var dbContext = CreateDbContext(SafeConnectionString);
        var service = CreateHostedService(dbContext, migration, seeding, applyMigrations: true);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.Equal(["compiled", "pending", "migrate"], operations);
        Assert.False(seeding.WasCalled);
    }

    [Fact]
    public async Task SeedingExceptionIsPropagated()
    {
        var operations = new List<string>();
        var expected = new InvalidOperationException("seeding failed");
        var migration = new RecordingMigrationService(operations);
        var seeding = new RecordingEnsureSeeding(operations, expected);
        await using var dbContext = CreateDbContext(SafeConnectionString);
        var service = CreateHostedService(dbContext, migration, seeding, applyMigrations: true);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.Equal(["compiled", "pending", "migrate", "seed"], operations);
    }

    [Fact]
    public async Task InitializationPropagatesCancellationToken()
    {
        var operations = new List<string>();
        var migration = new RecordingMigrationService(operations);
        var seeding = new RecordingEnsureSeeding(operations);
        await using var dbContext = CreateDbContext(SafeConnectionString);
        var service = CreateHostedService(dbContext, migration, seeding, applyMigrations: true);
        using var cancellation = new CancellationTokenSource();

        await service.StartAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, migration.PendingCancellationToken);
        Assert.Equal(cancellation.Token, migration.MigrationCancellationToken);
        Assert.Equal(cancellation.Token, seeding.CancellationToken);
    }

    [Fact]
    public async Task InitializationIsSkippedWhenAutomaticMigrationsAreDisabled()
    {
        var operations = new List<string>();
        var migration = new RecordingMigrationService(operations);
        var seeding = new RecordingEnsureSeeding(operations);
        await using var dbContext = CreateDbContext(SafeConnectionString);
        var logger = new ListLogger<DatabaseInitializationHostedService>();
        var service = CreateHostedService(
            dbContext,
            migration,
            seeding,
            applyMigrations: false,
            logger);

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.Empty(operations);
        Assert.False(seeding.WasCalled);
        Assert.Contains(
            logger.Messages,
            message => message == "Database migration and seeding skipped because ApplyMigrationsOnStartup is disabled.");
    }

    [Fact]
    public async Task StartupLogsDoNotContainSensitiveConnectionStringValues()
    {
        const string connectionString =
            "Server=sql.example.local;Database=SafeDatabase;User ID=sensitive-user;Password=top-secret;TrustServerCertificate=True";
        var operations = new List<string>();
        var migration = new RecordingMigrationService(operations);
        var seeding = new RecordingEnsureSeeding(operations);
        await using var dbContext = CreateDbContext(connectionString);
        var logger = new ListLogger<DatabaseInitializationHostedService>();
        var service = CreateHostedService(
            dbContext,
            migration,
            seeding,
            applyMigrations: false,
            logger);

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.Contains(logger.Messages, message => message == "Database server: sql.example.local.");
        Assert.Contains(logger.Messages, message => message == "Database name: SafeDatabase.");
        Assert.Contains(logger.Messages, message => message == "Integrated security enabled: False.");
        Assert.DoesNotContain(logger.Messages, message => message.Contains("sensitive-user", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("top-secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LaterSeedersDoNotExecuteAfterSeederFailure()
    {
        var operations = new List<string>();
        var expected = new InvalidOperationException("first seeder failed");
        ISeeder[] seeders =
        [
            new FailingSeeder(operations, expected),
            new LaterSeeder(operations)
        ];
        var ensureSeeding = new EnsureSeeding(seeders, new ListLogger<EnsureSeeding>());

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ensureSeeding.SeedDatabaseAsync(TestContext.Current.CancellationToken));

        Assert.Same(expected, actual);
        Assert.Equal(["failing"], operations);
    }

    [Fact]
    public async Task SeedersExecuteAccordingToExecutionOrder()
    {
        var operations = new List<string>();
        ISeeder[] seeders =
        [
            new LaterSeeder(operations),
            new EarlySeeder(operations)
        ];
        var ensureSeeding = new EnsureSeeding(seeders, new ListLogger<EnsureSeeding>());

        await ensureSeeding.SeedDatabaseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["early", "later"], operations);
    }

    [Fact]
    public async Task EqualExecutionOrdersUseSeederTypeNameAsSecondaryOrder()
    {
        var operations = new List<string>();
        ISeeder[] seeders =
        [
            new ZetaSeeder(operations),
            new AlphaSeeder(operations)
        ];
        var ensureSeeding = new EnsureSeeding(seeders, new ListLogger<EnsureSeeding>());

        await ensureSeeding.SeedDatabaseAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["alpha", "zeta"], operations);
    }

    [Fact]
    public async Task EnsureSeedingPropagatesCancellationTokenToEverySeeder()
    {
        var tokens = new List<CancellationToken>();
        ISeeder[] seeders =
        [
            new TokenSeeder(tokens, executionOrder: 20),
            new TokenSeeder(tokens, executionOrder: 10)
        ];
        var ensureSeeding = new EnsureSeeding(seeders, new ListLogger<EnsureSeeding>());
        using var cancellation = new CancellationTokenSource();

        await ensureSeeding.SeedDatabaseAsync(cancellation.Token);

        Assert.Equal(2, tokens.Count);
        Assert.All(tokens, token => Assert.Equal(cancellation.Token, token));
    }

    [Fact]
    public async Task DisabledSuperAdminSeederSkipsWithoutAccessingDatabase()
    {
        var seeder = new SuperAdminSeeder(
            accountReader: null!,
            unitOfWork: null!,
            Options.Create(new InitialSuperAdminOptions
            {
                Enabled = false
            }),
            normalizer: null!,
            passwordService: null!,
            clock: null!);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);
    }

    private static LawyerPlatformDbContext CreateDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new LawyerPlatformDbContext(options);
    }

    private static DatabaseInitializationHostedService CreateHostedService(
        LawyerPlatformDbContext dbContext,
        IDatabaseMigrationService migration,
        IEnsureSeeding seeding,
        bool applyMigrations,
        ILogger<DatabaseInitializationHostedService>? logger = null)
    {
        var provider = new DictionaryServiceProvider(
            new Dictionary<Type, object>
            {
                [typeof(LawyerPlatformDbContext)] = dbContext,
                [typeof(IDatabaseMigrationService)] = migration,
                [typeof(IEnsureSeeding)] = seeding
            });

        return new DatabaseInitializationHostedService(
            new TestScopeFactory(provider),
            Options.Create(new DatabaseInitializationOptions
            {
                ApplyMigrationsOnStartup = applyMigrations
            }),
            new TestHostEnvironment(),
            logger ?? new ListLogger<DatabaseInitializationHostedService>());
    }

    private sealed class RecordingMigrationService(
        List<string> operations,
        Exception? migrationException = null)
        : IDatabaseMigrationService
    {
        public CancellationToken PendingCancellationToken { get; private set; }
        public CancellationToken MigrationCancellationToken { get; private set; }

        public IReadOnlyList<string> GetCompiledMigrations(LawyerPlatformDbContext dbContext)
        {
            operations.Add("compiled");
            return ["20260101000000_InitialCreate"];
        }

        public Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
            LawyerPlatformDbContext dbContext,
            CancellationToken cancellationToken)
        {
            operations.Add("pending");
            PendingCancellationToken = cancellationToken;
            return Task.FromResult<IReadOnlyList<string>>(["20260101000000_InitialCreate"]);
        }

        public Task MigrateAsync(
            LawyerPlatformDbContext dbContext,
            CancellationToken cancellationToken)
        {
            operations.Add("migrate");
            MigrationCancellationToken = cancellationToken;
            return migrationException is null
                ? Task.CompletedTask
                : Task.FromException(migrationException);
        }
    }

    private sealed class RecordingEnsureSeeding(
        List<string> operations,
        Exception? exception = null)
        : IEnsureSeeding
    {
        public bool WasCalled { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public Task SeedDatabaseAsync(CancellationToken cancellationToken)
        {
            WasCalled = true;
            CancellationToken = cancellationToken;
            operations.Add("seed");
            return exception is null
                ? Task.CompletedTask
                : Task.FromException(exception);
        }
    }

    private sealed class FailingSeeder(List<string> operations, Exception exception) : ISeeder
    {
        public int ExecutionOrder => 10;

        public Task SeedAsync(CancellationToken cancellationToken)
        {
            operations.Add("failing");
            return Task.FromException(exception);
        }
    }

    private sealed class EarlySeeder(List<string> operations) : ISeeder
    {
        public int ExecutionOrder => 10;

        public Task SeedAsync(CancellationToken cancellationToken)
        {
            operations.Add("early");
            return Task.CompletedTask;
        }
    }

    private sealed class LaterSeeder(List<string> operations) : ISeeder
    {
        public int ExecutionOrder => 20;

        public Task SeedAsync(CancellationToken cancellationToken)
        {
            operations.Add("later");
            return Task.CompletedTask;
        }
    }

    private sealed class AlphaSeeder(List<string> operations) : ISeeder
    {
        public int ExecutionOrder => 10;

        public Task SeedAsync(CancellationToken cancellationToken)
        {
            operations.Add("alpha");
            return Task.CompletedTask;
        }
    }

    private sealed class ZetaSeeder(List<string> operations) : ISeeder
    {
        public int ExecutionOrder => 10;

        public Task SeedAsync(CancellationToken cancellationToken)
        {
            operations.Add("zeta");
            return Task.CompletedTask;
        }
    }

    private sealed class TokenSeeder(List<CancellationToken> tokens, int executionOrder) : ISeeder
    {
        public int ExecutionOrder => executionOrder;

        public Task SeedAsync(CancellationToken cancellationToken)
        {
            tokens.Add(cancellationToken);
            return Task.CompletedTask;
        }
    }

    private sealed class DictionaryServiceProvider(IReadOnlyDictionary<Type, object> services) : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => services.GetValueOrDefault(serviceType);
    }

    private sealed class TestScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
            => new TestScope(serviceProvider);
    }

    private sealed class TestScope(IServiceProvider serviceProvider) : IServiceScope, IAsyncDisposable
    {
        public IServiceProvider ServiceProvider => serviceProvider;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LawyerPlatform.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
