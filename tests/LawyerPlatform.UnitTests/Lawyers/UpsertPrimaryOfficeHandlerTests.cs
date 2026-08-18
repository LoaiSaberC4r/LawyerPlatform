using System.Linq.Expressions;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.UpsertPrimaryOffice;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.UnitTests.Lawyers;

public sealed class UpsertPrimaryOfficeHandlerTests
{
    private static readonly byte[] LatestRowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task CreateWithCoordinates_PersistsAndReturnsCoordinatesWithOneSave()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            Command(30.044420m, 31.235712m, null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(30.044420m, result.Value.Latitude);
        Assert.Equal(31.235712m, result.Value.Longitude);
        Assert.Equal(Convert.ToBase64String(LatestRowVersion), result.Value.RowVersion);
        Assert.Equal(1, fixture.UnitOfWork.SaveCalls);
        Assert.Same(Assert.Single(fixture.Profile.Offices), fixture.Persistence.AddedOffice);
    }

    [Fact]
    public async Task CreateWithoutCoordinates_PreservesOptionalBehavior()
    {
        var fixture = CreateFixture();

        var result = await fixture.Handler.Handle(
            Command(null, null, null),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Latitude);
        Assert.Null(result.Value.Longitude);
        Assert.Equal(1, fixture.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task UpdateUsesConcurrencyTokenChangesCoordinatesAndPreservesOfficeIdentity()
    {
        var profile = CreateProfile();
        var existing = profile.UpsertPrimaryOffice(
            1, 10, 100, "Original office", null, 30m, 31m).Value;
        var fixture = CreateFixture(profile);
        var suppliedRowVersion = new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 };

        var result = await fixture.Handler.Handle(
            Command(29.975300m, 31.137600m, Convert.ToBase64String(suppliedRowVersion)),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.Id);
        Assert.Equal(29.975300m, existing.Latitude);
        Assert.Equal(31.137600m, existing.Longitude);
        Assert.Equal(Convert.ToBase64String(LatestRowVersion), result.Value.RowVersion);
        Assert.Equal(suppliedRowVersion, fixture.Concurrency.RowVersion);
        Assert.Same(existing, fixture.Concurrency.Entity);
        Assert.Null(fixture.Persistence.AddedOffice);
        Assert.Equal(1, fixture.UnitOfWork.SaveCalls);
    }

    private static HandlerFixture CreateFixture(LawyerProfile? profile = null)
    {
        profile ??= CreateProfile();
        var unitOfWork = new RecordingUnitOfWork(new ProfileWriteRepository(profile));
        var persistence = new RecordingAggregatePersistence();
        var concurrency = new RecordingConcurrencyTokenManager();
        var hierarchyReader = new StubReadRepository<Area>(type =>
            type == typeof(LocationHierarchySnapshot)
                ? new LocationHierarchySnapshot(100, true, 10, true, 1, true)
                : null);
        var officeReader = new StubReadRepository<LawyerProfile>(type =>
            type == typeof(PrimaryOfficeSnapshot)
                ? CreateOfficeSnapshot(profile, LatestRowVersion)
                : null);
        var handler = new UpsertPrimaryOfficeCommandHandler(
            new FakeCurrentUser(profile.UserAccountId),
            unitOfWork,
            hierarchyReader,
            officeReader,
            concurrency,
            persistence,
            new FixedClock(new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc)));
        return new HandlerFixture(profile, handler, unitOfWork, persistence, concurrency);
    }

    private static UpsertPrimaryOfficeCommand Command(
        decimal? latitude,
        decimal? longitude,
        string? rowVersion)
        => new(1, 10, 100, "Updated office", "01012345678", latitude, longitude, rowVersion);

    private static PrimaryOfficeSnapshot CreateOfficeSnapshot(LawyerProfile profile, byte[] rowVersion)
    {
        var office = Assert.Single(profile.Offices);
        return new PrimaryOfficeSnapshot(
            office.Id,
            office.GovernorateId,
            "القاهرة",
            "Cairo",
            office.CityId,
            "مدينة",
            "City",
            office.AreaId,
            "منطقة",
            "Area",
            office.DetailedAddress,
            office.PublicPhoneNumber,
            office.Latitude,
            office.Longitude,
            rowVersion);
    }

    private static LawyerProfile CreateProfile()
    {
        var account = UserAccount.CreateLawyer(
            "office.handler",
            "OFFICE.HANDLER",
            "office.handler@example.test",
            "OFFICE.HANDLER@EXAMPLE.TEST",
            "01012345678",
            "hash",
            new DateTime(2026, 8, 18, 8, 0, 0, DateTimeKind.Utc)).Value;
        return LawyerProfile.Create(account, "Office Handler Lawyer").Value;
    }

    private sealed record HandlerFixture(
        LawyerProfile Profile,
        UpsertPrimaryOfficeCommandHandler Handler,
        RecordingUnitOfWork UnitOfWork,
        RecordingAggregatePersistence Persistence,
        RecordingConcurrencyTokenManager Concurrency);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "office.handler";
        public string? Email => "office.handler@example.test";
        public IReadOnlyCollection<string> Roles => ["Lawyer"];
        public IReadOnlyCollection<string> Permissions => [];
        public string? GetClaimValue(string claimType) => null;
        public IReadOnlyCollection<string> GetClaimValues(string claimType) => [];
    }

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class RecordingConcurrencyTokenManager : IConcurrencyTokenManager
    {
        public object? Entity { get; private set; }
        public byte[]? RowVersion { get; private set; }

        public void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : class
        {
            Entity = entity;
            RowVersion = rowVersion;
        }

        public void MarkPropertyModified<TEntity, TProperty>(
            TEntity entity,
            Expression<Func<TEntity, TProperty>> propertyExpression)
            where TEntity : class
            => throw new NotSupportedException();
    }

    private sealed class RecordingAggregatePersistence : ILawyerAggregatePersistence
    {
        public LawyerOffice? AddedOffice { get; private set; }

        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
            AddedOffice = entity as LawyerOffice;
        }

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
            => throw new NotSupportedException();

        public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
            => throw new NotSupportedException();
    }

    private sealed class RecordingUnitOfWork(ProfileWriteRepository repository)
        : IUnitOfWork<LawyerPlatformWritePersistence>
    {
        public int SaveCalls { get; private set; }

        public IWriteRepository<TEntity, LawyerPlatformWritePersistence> WriteRepository<TEntity>()
            where TEntity : class, BuildingBlock.Domain.Primitive.IAggregateRoot
            => (IWriteRepository<TEntity, LawyerPlatformWritePersistence>)(object)repository;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }

        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ProfileWriteRepository(LawyerProfile profile)
        : IWriteRepository<LawyerProfile, LawyerPlatformWritePersistence>
    {
        public Task<LawyerProfile?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
            where TKey : notnull
            => Task.FromResult<LawyerProfile?>(profile);

        public Task<LawyerProfile?> GetByPropertyAsync(
            Expression<Func<LawyerProfile, bool>> predicate,
            CancellationToken cancellationToken = default)
            => Task.FromResult<LawyerProfile?>(profile);

        public Task<LawyerProfile?> FirstOrDefaultAsync(
            Specification<LawyerProfile> specification,
            CancellationToken cancellationToken = default)
            => Task.FromResult<LawyerProfile?>(profile);

        public Task AddAsync(LawyerProfile entity, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<LawyerProfile> entities, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public void Update(LawyerProfile entity) => throw new NotSupportedException();
        public void UpdateRange(IEnumerable<LawyerProfile> entities) => throw new NotSupportedException();
        public void Delete(LawyerProfile entity) => throw new NotSupportedException();
        public void DeleteRange(IEnumerable<LawyerProfile> entities) => throw new NotSupportedException();
    }

    private sealed class StubReadRepository<TEntity>(Func<Type, object?> projection)
        : IReadRepository<TEntity, LawyerPlatformReadPersistence>
        where TEntity : class
    {
        public Task<TOut?> FirstOrDefaultAsync<TOut>(
            Specification<TEntity, TOut> specification,
            CancellationToken cancellationToken = default)
        {
            var value = projection(typeof(TOut));
            return Task.FromResult(value is null ? default : (TOut)value);
        }

        public Task<TEntity?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
            where TKey : notnull
            => throw new NotSupportedException();
        public Task<TEntity?> GetByPropertyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<long> LongCountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<TEntity>> ListAsync(Specification<TEntity> specification, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<TEntity?> FirstOrDefaultAsync(Specification<TEntity> specification, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<TOut>> ListAsync<TOut>(Specification<TEntity, TOut> specification, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<(IReadOnlyList<TEntity> Items, long TotalCount)> ListWithLongCountAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<(IReadOnlyList<TOut> Items, long TotalCount)> ListWithLongCountAsync<TOut>(
            Specification<TEntity, TOut> specification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
