using LawyerPlatform.Domain.ReferenceData;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.IntegrationTests.Seeding;

public sealed class EgyptLocationSeedingTests
{
    [Fact]
    public async Task EmptyDatabaseReceivesEveryApprovedRecord()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        await SeedAllAsync(context, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        Assert.Equal(EgyptLocationSeedCatalog.ExpectedGovernorateCount, await context.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(EgyptLocationSeedCatalog.ExpectedCityCount, await context.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(EgyptLocationSeedCatalog.ExpectedAreaCount, await context.Areas.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.Governorates.CountAsync(item => !item.IsActive, TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.Cities.CountAsync(item => !item.IsActive, TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.Areas.CountAsync(item => !item.IsActive, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SecondExecutionCreatesNoDuplicateRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();

        await SeedAllAsync(context, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        await SeedAllAsync(context, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        Assert.Equal(EgyptLocationSeedCatalog.ExpectedGovernorateCount, await context.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(EgyptLocationSeedCatalog.ExpectedCityCount, await context.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(EgyptLocationSeedCatalog.ExpectedAreaCount, await context.Areas.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistingExactRowIsSkipped()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var seed = EgyptLocationSeedCatalog.Governorates[0];
        context.Governorates.Add(CreateGovernorate(seed));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        await SeedAllAsync(context, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        Assert.Equal(1, await context.Governorates.CountAsync(item => item.Id == seed.Id, TestContext.Current.CancellationToken));
        var stored = await context.Governorates.SingleAsync(item => item.Id == seed.Id, TestContext.Current.CancellationToken);
        Assert.Equal(seed.NameAr, stored.NameAr);
        Assert.Equal(seed.NameEn, stored.NameEn);
    }

    [Fact]
    public async Task ExistingConflictingGovernorateFails()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var seed = EgyptLocationSeedCatalog.Governorates[0];
        context.Governorates.Add(
            Governorate.Create(seed.Id, "محافظة متعارضة", "Conflicting Governorate", 1).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        await using var coordinator = new EgyptLocationSeedCoordinator(context);
        await Assert.ThrowsAsync<EgyptLocationSeedConflictException>(
            () => coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken));

        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.Governorates.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistingConflictingCityFailsAndRollsBackGovernorateAdditions()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var governorateSeed = EgyptLocationSeedCatalog.Governorates[0];
        var citySeed = EgyptLocationSeedCatalog.Cities.First(
            seed => seed.GovernorateId == governorateSeed.Id);
        context.Governorates.Add(CreateGovernorate(governorateSeed));
        context.Cities.Add(
            City.Create(
                citySeed.Id,
                citySeed.GovernorateId,
                "مدينة متعارضة",
                "Conflicting City",
                citySeed.DisplayOrder).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        await using var coordinator = new EgyptLocationSeedCoordinator(context);
        await coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<EgyptLocationSeedConflictException>(
            () => coordinator.SeedCitiesAsync(TestContext.Current.CancellationToken));

        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await verification.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await verification.Areas.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistingConflictingAreaFailsAndRollsBackEarlierLevelAdditions()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var areaSeed = EgyptLocationSeedCatalog.Areas[0];
        var citySeed = EgyptLocationSeedCatalog.Cities.Single(seed => seed.Id == areaSeed.CityId);
        var governorateSeed = EgyptLocationSeedCatalog.Governorates.Single(
            seed => seed.Id == citySeed.GovernorateId);
        context.Governorates.Add(CreateGovernorate(governorateSeed));
        context.Cities.Add(CreateCity(citySeed));
        context.Areas.Add(
            Area.Create(
                areaSeed.Id,
                areaSeed.CityId,
                "منطقة متعارضة",
                "Conflicting Area",
                areaSeed.DisplayOrder).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        await using var coordinator = new EgyptLocationSeedCoordinator(context);
        await coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken);
        await coordinator.SeedCitiesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<EgyptLocationSeedConflictException>(
            () => coordinator.SeedAreasAsync(TestContext.Current.CancellationToken));

        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await verification.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await verification.Areas.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SameNormalizedNameUnderParentWithDifferentIdFails()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var seed = EgyptLocationSeedCatalog.Governorates[0];
        context.Governorates.Add(
            Governorate.Create(999, seed.NameAr, seed.NameEn.ToUpperInvariant(), 1).Value);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        await using var coordinator = new EgyptLocationSeedCoordinator(context);
        await Assert.ThrowsAsync<EgyptLocationSeedConflictException>(
            () => coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingParentGovernorateFailsBeforeAnyWrite()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var cities = EgyptLocationSeedCatalog.Cities.ToArray();
        cities[0] = cities[0] with { GovernorateId = int.MaxValue };
        var catalog = new EgyptLocationSeedData(
            EgyptLocationSeedCatalog.Governorates,
            cities,
            EgyptLocationSeedCatalog.Areas);
        await using var coordinator = new EgyptLocationSeedCoordinator(context, catalog);

        await Assert.ThrowsAsync<EgyptLocationSeedDataException>(
            () => coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0, await context.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.Areas.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingParentCityFailsBeforeAnyWrite()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var areas = EgyptLocationSeedCatalog.Areas.ToArray();
        areas[0] = areas[0] with { CityId = int.MaxValue };
        var catalog = new EgyptLocationSeedData(
            EgyptLocationSeedCatalog.Governorates,
            EgyptLocationSeedCatalog.Cities,
            areas);
        await using var coordinator = new EgyptLocationSeedCoordinator(context, catalog);

        await Assert.ThrowsAsync<EgyptLocationSeedDataException>(
            () => coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(0, await context.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await context.Areas.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ManualInactiveAndDisplayOrderChangesArePreserved()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var seed = EgyptLocationSeedCatalog.Governorates[0];
        await SeedAllAsync(context, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Governorates SET IsActive = 0, DisplayOrder = 999 WHERE Id = {seed.Id}",
            TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        await SeedAllAsync(context, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        var stored = await context.Governorates.SingleAsync(item => item.Id == seed.Id, TestContext.Current.CancellationToken);
        Assert.False(stored.IsActive);
        Assert.Equal(999, stored.DisplayOrder);
    }

    [Fact]
    public async Task CancellationTokenIsObservedBeforeDatabaseWork()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await using var coordinator = new EgyptLocationSeedCoordinator(context);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.SeedGovernoratesAsync(cancellation.Token));

        Assert.Equal(0, await context.Governorates.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AreaInsertFailureRollsBackNewGovernoratesAndCities()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER FailEgyptAreaInsert BEFORE INSERT ON Areas " +
            "BEGIN SELECT RAISE(FAIL, 'forced area insert failure'); END;",
            TestContext.Current.CancellationToken);
        await using var coordinator = new EgyptLocationSeedCoordinator(context);

        await coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken);
        await coordinator.SeedCitiesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateException>(
            () => coordinator.SeedAreasAsync(TestContext.Current.CancellationToken));

        await using var verification = database.CreateContext();
        Assert.Equal(0, await verification.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await verification.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await verification.Areas.CountAsync(TestContext.Current.CancellationToken));
    }

    private static async Task SeedAllAsync(
        LawyerPlatformDbContext context,
        CancellationToken cancellationToken)
    {
        await using var coordinator = new EgyptLocationSeedCoordinator(context);
        await coordinator.SeedGovernoratesAsync(cancellationToken);
        await coordinator.SeedCitiesAsync(cancellationToken);
        await coordinator.SeedAreasAsync(cancellationToken);
    }

    private static Governorate CreateGovernorate(GovernorateSeed seed)
        => Governorate.Create(seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder).Value;

    private static City CreateCity(CitySeed seed)
        => City.Create(
            seed.Id,
            seed.GovernorateId,
            seed.NameAr,
            seed.NameEn,
            seed.DisplayOrder).Value;

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private DbContextOptions<LawyerPlatformDbContext> options = null!;

        public static async Task<SqliteTestDatabase> CreateAsync()
        {
            var database = new SqliteTestDatabase();
            await database.connection.OpenAsync(TestContext.Current.CancellationToken);
            database.options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
                .UseSqlite(database.connection)
                .Options;
            await using var context = database.CreateContext();
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return database;
        }

        public LawyerPlatformDbContext CreateContext() => new(options);

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
