using System.Linq.Expressions;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Infrastructure.Options;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.UnitTests.Seeding;

public sealed class SuperAdminSeederTests
{
    private static readonly Guid SuperAdminId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly DateTime FixedUtcNow =
        new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DisabledSeederDoesNotQueryOrInsert()
    {
        var reader = new InMemoryAccountReader([]);
        var unitOfWork = new RecordingUnitOfWork();
        var passwordService = new RecordingPasswordService();
        var logger = new ListLogger<SuperAdminSeeder>();
        var seeder = CreateSeeder(
            enabled: false,
            reader,
            unitOfWork,
            passwordService,
            logger);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, reader.QueryCount);
        Assert.Empty(unitOfWork.Repository.AddedAccounts);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
        Assert.Equal(0, passwordService.HashCallCount);
        Assert.Contains(
            "Initial SuperAdmin seeding is disabled by configuration.",
            logger.Messages);
    }

    [Fact]
    public async Task EnabledSeederInsertsAccountWhenNoneExists()
    {
        var reader = new InMemoryAccountReader([]);
        var unitOfWork = new RecordingUnitOfWork();
        var passwordService = new RecordingPasswordService();
        var logger = new ListLogger<SuperAdminSeeder>();
        var seeder = CreateSeeder(
            enabled: true,
            reader,
            unitOfWork,
            passwordService,
            logger);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        var inserted = Assert.Single(unitOfWork.Repository.AddedAccounts);
        Assert.Equal(SuperAdminId, inserted.Id);
        Assert.Equal(AccountRole.SuperAdmin, inserted.Role);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
        Assert.Equal(1, passwordService.HashCallCount);
        Assert.Contains("SuperAdmin created or already exists.", logger.Messages);
    }

    [Fact]
    public async Task EnabledSeederDoesNotDuplicateMatchingExistingAccount()
    {
        var existing = CreateExistingSuperAdmin();
        var reader = new InMemoryAccountReader([existing]);
        var unitOfWork = new RecordingUnitOfWork();
        var passwordService = new RecordingPasswordService();
        var logger = new ListLogger<SuperAdminSeeder>();
        var seeder = CreateSeeder(
            enabled: true,
            reader,
            unitOfWork,
            passwordService,
            logger);

        await seeder.SeedAsync(TestContext.Current.CancellationToken);

        Assert.Empty(unitOfWork.Repository.AddedAccounts);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
        Assert.Equal(0, passwordService.HashCallCount);
        Assert.Contains("SuperAdmin created or already exists.", logger.Messages);
    }

    private static SuperAdminSeeder CreateSeeder(
        bool enabled,
        InMemoryAccountReader reader,
        RecordingUnitOfWork unitOfWork,
        RecordingPasswordService passwordService,
        ILogger<SuperAdminSeeder> logger)
        => new(
            reader,
            unitOfWork,
            Options.Create(new InitialSuperAdminOptions
            {
                Enabled = enabled,
                Id = SuperAdminId,
                UserName = "superadmin",
                Email = "admin@lawyerplatform.test",
                PhoneNumber = "01000000000",
                Password = "InitialPassword1!"
            }),
            new UpperInvariantAccountIdentifierNormalizer(),
            passwordService,
            new FixedClock(),
            logger);

    private static UserAccount CreateExistingSuperAdmin()
        => UserAccount.CreateSuperAdmin(
            SuperAdminId,
            "superadmin",
            "SUPERADMIN",
            "admin@lawyerplatform.test",
            "ADMIN@LAWYERPLATFORM.TEST",
            "01000000000",
            "existing-password-hash",
            FixedUtcNow).Value;

    private sealed class InMemoryAccountReader(IReadOnlyList<UserAccount> accounts)
        : IReadRepository<UserAccount, LawyerPlatformReadPersistence>
    {
        public int QueryCount { get; private set; }

        public Task<UserAccount?> GetByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            QueryCount++;
            return Task.FromResult(
                accounts.SingleOrDefault(account => account.Id.Equals(id)));
        }

        public Task<UserAccount?> GetByPropertyAsync(
            Expression<Func<UserAccount, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            QueryCount++;
            return Task.FromResult(accounts.SingleOrDefault(predicate.Compile()));
        }

        public Task<bool> AnyAsync(
            Expression<Func<UserAccount, bool>> predicate,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> LongCountAsync(
            Expression<Func<UserAccount, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<UserAccount>> ListAsync(
            Specification<UserAccount> specification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserAccount?> FirstOrDefaultAsync(
            Specification<UserAccount> specification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<TOut>> ListAsync<TOut>(
            Specification<UserAccount, TOut> specification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TOut?> FirstOrDefaultAsync<TOut>(
            Specification<UserAccount, TOut> specification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<UserAccount> Items, long TotalCount)> ListWithLongCountAsync(
            Specification<UserAccount> specification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyList<TOut> Items, long TotalCount)> ListWithLongCountAsync<TOut>(
            Specification<UserAccount, TOut> specification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingUnitOfWork
        : IUnitOfWork<LawyerPlatformWritePersistence>
    {
        public RecordingUserAccountWriteRepository Repository { get; } = new();
        public int SaveChangesCount { get; private set; }

        public IWriteRepository<TEntity, LawyerPlatformWritePersistence> WriteRepository<TEntity>()
            where TEntity : class, BuildingBlock.Domain.Primitive.IAggregateRoot
        {
            if (typeof(TEntity) != typeof(UserAccount))
            {
                throw new NotSupportedException();
            }

            return (IWriteRepository<TEntity, LawyerPlatformWritePersistence>)(object)Repository;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.FromResult(1);
        }

        public Task<IApplicationTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingUserAccountWriteRepository
        : IWriteRepository<UserAccount, LawyerPlatformWritePersistence>
    {
        public List<UserAccount> AddedAccounts { get; } = [];

        public Task<UserAccount?> GetByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull
            => throw new NotSupportedException();

        public Task<UserAccount?> GetByPropertyAsync(
            Expression<Func<UserAccount, bool>> predicate,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserAccount?> FirstOrDefaultAsync(
            Specification<UserAccount> specification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddAsync(
            UserAccount entity,
            CancellationToken cancellationToken = default)
        {
            AddedAccounts.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(
            IEnumerable<UserAccount> entities,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Update(UserAccount entity)
            => throw new NotSupportedException();

        public void UpdateRange(IEnumerable<UserAccount> entities)
            => throw new NotSupportedException();

        public void Delete(UserAccount entity)
            => throw new NotSupportedException();

        public void DeleteRange(IEnumerable<UserAccount> entities)
            => throw new NotSupportedException();
    }

    private sealed class UpperInvariantAccountIdentifierNormalizer
        : IAccountIdentifierNormalizer
    {
        public string NormalizeUserName(string userName)
            => userName.Trim().ToUpperInvariant();

        public string NormalizeEmail(string email)
            => email.Trim().ToUpperInvariant();
    }

    private sealed class RecordingPasswordService : IPasswordService
    {
        public int HashCallCount { get; private set; }

        public string Hash(string password)
            => "password-hash";

        public bool Verify(string password, string passwordHash)
            => throw new NotSupportedException();

        public PasswordVerification VerifyDetailed(string password, string passwordHash)
            => throw new NotSupportedException();

        public Task<string> HashAsync(
            string password,
            CancellationToken ct = default)
        {
            HashCallCount++;
            return Task.FromResult("password-hash");
        }

        public Task<bool> VerifyAsync(
            string password,
            string passwordHash,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public bool IsStrongPassword(string password)
            => true;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime UtcNow => FixedUtcNow;
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
