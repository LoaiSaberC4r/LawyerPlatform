using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace LawyerPlatform.IntegrationTests;

[Collection(LawyerPlatformSqlServerTestGroup.Name)]
public sealed class LawyerOfficeCoordinatesSqlServerTests(LawyerPlatformSqlServerFixture fixture)
{
    [Fact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CoordinatesUseNullableDecimalColumnsAndPersistExactValues()
    {
        await using (var seedContext = fixture.CreateContext())
        await using (var coordinator = new EgyptLocationSeedCoordinator(seedContext))
        {
            await coordinator.SeedGovernoratesAsync(TestContext.Current.CancellationToken);
            await coordinator.SeedCitiesAsync(TestContext.Current.CancellationToken);
            await coordinator.SeedAreasAsync(TestContext.Current.CancellationToken);
        }

        var area = EgyptLocationSeedCatalog.Areas[0];
        var city = EgyptLocationSeedCatalog.Cities.Single(item => item.Id == area.CityId);
        var withCoordinates = CreateProfile("coordinates", "01076543210");
        var withoutCoordinates = CreateProfile("null-coordinates", "01176543210");
        Assert.True(withCoordinates.UpsertPrimaryOffice(
            city.GovernorateId,
            city.Id,
            area.Id,
            "Coordinate office",
            null,
            30.044420m,
            31.235712m).IsSuccess);
        Assert.True(withoutCoordinates.UpsertPrimaryOffice(
            city.GovernorateId,
            city.Id,
            area.Id,
            "Office without coordinates",
            null,
            null,
            null).IsSuccess);

        await using (var context = fixture.CreateContext())
        {
            context.LawyerProfiles.AddRange(withCoordinates, withoutCoordinates);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = fixture.CreateContext())
        {
            var storedWithCoordinates = await context.LawyerOffices
                .AsNoTracking()
                .SingleAsync(
                    office => office.LawyerProfileId == withCoordinates.Id,
                    TestContext.Current.CancellationToken);
            var storedWithoutCoordinates = await context.LawyerOffices
                .AsNoTracking()
                .SingleAsync(
                    office => office.LawyerProfileId == withoutCoordinates.Id,
                    TestContext.Current.CancellationToken);

            Assert.Equal(30.044420m, storedWithCoordinates.Latitude);
            Assert.Equal(31.235712m, storedWithCoordinates.Longitude);
            Assert.Null(storedWithoutCoordinates.Latitude);
            Assert.Null(storedWithoutCoordinates.Longitude);
        }

        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COLUMN_NAME, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = 'LawyerOffices' AND COLUMN_NAME IN ('Latitude', 'Longitude')
            ORDER BY COLUMN_NAME
            """;
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var columns = new List<(string Name, int Precision, int Scale, string Nullable)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add((
                reader.GetString(0),
                Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.GetString(3)));
        }

        Assert.Equal(2, columns.Count);
        Assert.All(columns, column =>
        {
            Assert.Equal(9, column.Precision);
            Assert.Equal(6, column.Scale);
            Assert.Equal("YES", column.Nullable);
        });
    }

    private static LawyerProfile CreateProfile(string prefix, string phoneNumber)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var account = UserAccount.CreateLawyer(
            $"{prefix}-{suffix[..8]}",
            $"{prefix.ToUpperInvariant()}-{suffix[..8].ToUpperInvariant()}",
            $"{prefix}-{suffix}@example.test",
            $"{prefix.ToUpperInvariant()}-{suffix.ToUpperInvariant()}@EXAMPLE.TEST",
            phoneNumber,
            "integration-test-hash",
            DateTime.UtcNow).Value;
        return LawyerProfile.Create(account, $"{prefix} Lawyer").Value;
    }
}
