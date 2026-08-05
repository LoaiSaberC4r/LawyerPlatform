using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace LawyerPlatform.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LawyerPlatformSqlServerTestGroup : ICollectionFixture<LawyerPlatformSqlServerFixture>
{
    public const string Name = "LawyerPlatformSqlServer";
}

[Collection(LawyerPlatformSqlServerTestGroup.Name)]
public sealed class SqlServerIdentityPersistenceTests(LawyerPlatformSqlServerFixture fixture)
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Migration_CreatesAllIdentityAndReferenceTables()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Contains("UserAccounts", tables);
        Assert.Contains("ClientProfiles", tables);
        Assert.Contains("LawyerProfiles", tables);
        Assert.Contains("Governorates", tables);
        Assert.Contains("Cities", tables);
        Assert.Contains("Areas", tables);
        Assert.Contains("LegalSpecializations", tables);
        Assert.Contains("LawyerOffices", tables);
        Assert.Contains("LawyerSpecializations", tables);
        Assert.Contains("LawyerDocuments", tables);
        Assert.Contains("LawyerApprovalStatusHistory", tables);
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task UniqueRestrictAndRowVersionConstraints_AreEnforcedBySqlServer()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var first = CreateClientAccount(
            $"client-{suffix[..8]}",
            $"CLIENT-{suffix[..8]}",
            $"{suffix}@example.test",
            $"{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            $"01{suffix[..18]}");
        await using (var seedContext = fixture.CreateContext())
        {
            seedContext.UserAccounts.Add(first);
            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var duplicateContext = fixture.CreateContext())
        {
            duplicateContext.UserAccounts.Add(CreateClientAccount(
                $"other-{suffix[..8]}",
                first.NormalizedUserName,
                $"other-{suffix}@example.test",
                $"OTHER-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
                $"02{suffix[..18]}"));
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                duplicateContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        var profileAccount = CreateClientAccount(
            $"profile-{suffix[..8]}",
            $"PROFILE-{suffix[..8]}",
            $"profile-{suffix}@example.test",
            $"PROFILE-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            $"03{suffix[..18]}");
        var profile = ClientProfile.Create(profileAccount, "Constraint Client").Value;
        await using (var profileContext = fixture.CreateContext())
        {
            profileContext.AddRange(profileAccount, profile);
            await profileContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var deleteContext = fixture.CreateContext())
        {
            var trackedAccount = await deleteContext.UserAccounts.SingleAsync(
                account => account.Id == profileAccount.Id,
                TestContext.Current.CancellationToken);
            deleteContext.UserAccounts.Remove(trackedAccount);
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                deleteContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var firstCopy = await firstContext.UserAccounts.SingleAsync(
            account => account.Id == first.Id,
            TestContext.Current.CancellationToken);
        var secondCopy = await secondContext.UserAccounts.SingleAsync(
            account => account.Id == first.Id,
            TestContext.Current.CancellationToken);
        firstCopy.Suspend(DateTime.UtcNow);
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        secondCopy.Deactivate(DateTime.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            secondContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task MigratedSqlServerReceivesTheCompleteIdempotentEgyptHierarchy()
    {
        await using var context = fixture.CreateContext();
        await using (var coordinator = new EgyptLocationSeedCoordinator(context))
        {
            await coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken);
            await coordinator.SeedCitiesAsync(TestContext.Current.CancellationToken);
            await coordinator.SeedAreasAsync(TestContext.Current.CancellationToken);
        }

        context.ChangeTracker.Clear();
        await using (var coordinator = new EgyptLocationSeedCoordinator(context))
        {
            await coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken);
            await coordinator.SeedCitiesAsync(TestContext.Current.CancellationToken);
            await coordinator.SeedAreasAsync(TestContext.Current.CancellationToken);
        }

        context.ChangeTracker.Clear();
        Assert.Equal(
            EgyptLocationSeedCatalog.ExpectedGovernorateCount,
            await context.Governorates.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            EgyptLocationSeedCatalog.ExpectedCityCount,
            await context.Cities.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            EgyptLocationSeedCatalog.ExpectedAreaCount,
            await context.Areas.CountAsync(TestContext.Current.CancellationToken));
        Assert.False(
            await context.Cities.AnyAsync(
                city => !context.Governorates.Any(governorate => governorate.Id == city.GovernorateId),
                TestContext.Current.CancellationToken));
        Assert.False(
            await context.Areas.AnyAsync(
                area => !context.Cities.Any(city => city.Id == area.CityId),
                TestContext.Current.CancellationToken));
    }

    private static UserAccount CreateClientAccount(
        string userName,
        string normalizedUserName,
        string email,
        string normalizedEmail,
        string phoneNumber)
        => UserAccount.CreateClient(
            userName,
            normalizedUserName,
            email,
            normalizedEmail,
            phoneNumber,
            "integration-test-hash",
            DateTime.UtcNow).Value;
}

public sealed class LawyerPlatformSqlServerFixture : IAsyncLifetime
{
    private const string ConnectionStringVariable = "LAWYERPLATFORM_SQLSERVER_TEST_CONNECTION_STRING";
    private MsSqlContainer? _container;
    private string? _masterConnectionString;
    private string? _databaseName;

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync(TestContext.Current.CancellationToken);
            configured = _container.GetConnectionString();
        }

        var masterBuilder = new SqlConnectionStringBuilder(configured)
        {
            InitialCatalog = "master",
            TrustServerCertificate = true
        };
        _masterConnectionString = masterBuilder.ConnectionString;
        _databaseName = $"LawyerPlatformIdentityTests_{Guid.NewGuid():N}";

        await using (var connection = new SqlConnection(_masterConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        masterBuilder.InitialCatalog = _databaseName;
        ConnectionString = masterBuilder.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public LawyerPlatformDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new LawyerPlatformDbContext(options);
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        if (!string.IsNullOrWhiteSpace(_masterConnectionString) && !string.IsNullOrWhiteSpace(_databaseName))
        {
            await using var connection = new SqlConnection(_masterConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]";
            await command.ExecuteNonQueryAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
