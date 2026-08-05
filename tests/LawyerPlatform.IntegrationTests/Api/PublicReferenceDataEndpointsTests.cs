using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests.Api;

public sealed class PublicReferenceDataEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync()
        => await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GovernoratesReturnsAllApprovedActiveRecordsInStableOrder()
    {
        using var client = CreateClient();

        var records = await client.GetFromJsonAsync<List<LocationResponse>>(
            "/api/v1/public/governorates",
            TestContext.Current.CancellationToken);

        Assert.NotNull(records);
        Assert.Equal(EgyptLocationSeedCatalog.ExpectedGovernorateCount, records.Count);
        Assert.Equal(
            EgyptLocationSeedCatalog.Governorates
                .OrderBy(seed => seed.DisplayOrder)
                .ThenBy(seed => seed.Id)
                .Select(seed => seed.Id),
            records.Select(record => record.Id));
    }

    [Fact]
    public async Task CitiesReturnsOnlyActiveChildrenOfTheRequestedGovernorateInStableOrder()
    {
        var group = EgyptLocationSeedCatalog.Cities
            .GroupBy(seed => seed.GovernorateId)
            .First(items => items.Count() > 1);
        var inactive = group.Last();
        await SetCityActiveAsync(inactive.Id, isActive: false);

        try
        {
            using var client = CreateClient();
            var records = await client.GetFromJsonAsync<List<LocationResponse>>(
                $"/api/v1/public/governorates/{group.Key}/cities",
                TestContext.Current.CancellationToken);

            Assert.NotNull(records);
            Assert.Equal(
                group.Where(seed => seed.Id != inactive.Id)
                    .OrderBy(seed => seed.DisplayOrder)
                    .ThenBy(seed => seed.Id)
                    .Select(seed => seed.Id),
                records.Select(record => record.Id));
            Assert.DoesNotContain(records, record => record.Id == inactive.Id);
        }
        finally
        {
            await SetCityActiveAsync(inactive.Id, isActive: true);
        }
    }

    [Fact]
    public async Task AreasReturnsOnlyActiveChildrenOfTheRequestedCityInStableOrder()
    {
        var group = EgyptLocationSeedCatalog.Areas
            .GroupBy(seed => seed.CityId)
            .First(items => items.Count() > 1);
        var inactive = group.Last();
        await SetAreaActiveAsync(inactive.Id, isActive: false);

        try
        {
            using var client = CreateClient();
            var records = await client.GetFromJsonAsync<List<LocationResponse>>(
                $"/api/v1/public/cities/{group.Key}/areas",
                TestContext.Current.CancellationToken);

            Assert.NotNull(records);
            Assert.Equal(
                group.Where(seed => seed.Id != inactive.Id)
                    .OrderBy(seed => seed.DisplayOrder)
                    .ThenBy(seed => seed.Id)
                    .Select(seed => seed.Id),
                records.Select(record => record.Id));
            Assert.DoesNotContain(records, record => record.Id == inactive.Id);
        }
        finally
        {
            await SetAreaActiveAsync(inactive.Id, isActive: true);
        }
    }

    [Fact]
    public async Task InactiveGovernorateHierarchyIsRejectedWithStableCodes()
    {
        var governorate = EgyptLocationSeedCatalog.Governorates[0];
        var city = EgyptLocationSeedCatalog.Cities.First(
            seed => seed.GovernorateId == governorate.Id);
        await SetGovernorateActiveAsync(governorate.Id, isActive: false);

        try
        {
            using var client = CreateClient();
            var citiesResponse = await client.GetAsync(
                $"/api/v1/public/governorates/{governorate.Id}/cities",
                TestContext.Current.CancellationToken);
            var areasResponse = await client.GetAsync(
                $"/api/v1/public/cities/{city.Id}/areas",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, citiesResponse.StatusCode);
            Assert.Equal("Location.GovernorateNotFound", await ReadPrimaryCodeAsync(citiesResponse));
            Assert.Equal((HttpStatusCode)422, areasResponse.StatusCode);
            Assert.Equal("Location.InvalidHierarchy", await ReadPrimaryCodeAsync(areasResponse));
        }
        finally
        {
            await SetGovernorateActiveAsync(governorate.Id, isActive: true);
        }
    }

    [Fact]
    public async Task InactiveCityHierarchyIsRejectedWithStableCode()
    {
        var city = EgyptLocationSeedCatalog.Cities[0];
        await SetCityActiveAsync(city.Id, isActive: false);

        try
        {
            using var client = CreateClient();
            var response = await client.GetAsync(
                $"/api/v1/public/cities/{city.Id}/areas",
                TestContext.Current.CancellationToken);

            Assert.Equal((HttpStatusCode)422, response.StatusCode);
            Assert.Equal("Location.InvalidHierarchy", await ReadPrimaryCodeAsync(response));
        }
        finally
        {
            await SetCityActiveAsync(city.Id, isActive: true);
        }
    }

    [Fact]
    public async Task InvalidParentIdsReturnStableProblemDetailsCodes()
    {
        using var client = CreateClient();
        var citiesResponse = await client.GetAsync(
            "/api/v1/public/governorates/2147483647/cities",
            TestContext.Current.CancellationToken);
        var areasResponse = await client.GetAsync(
            "/api/v1/public/cities/2147483647/areas",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, citiesResponse.StatusCode);
        Assert.Equal("Location.GovernorateNotFound", await ReadPrimaryCodeAsync(citiesResponse));
        Assert.Equal(HttpStatusCode.NotFound, areasResponse.StatusCode);
        Assert.Equal("Location.CityNotFound", await ReadPrimaryCodeAsync(areasResponse));
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private async Task SetGovernorateActiveAsync(int id, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Governorates SET IsActive = {isActive} WHERE Id = {id}",
            TestContext.Current.CancellationToken);
    }

    private async Task SetCityActiveAsync(int id, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Cities SET IsActive = {isActive} WHERE Id = {id}",
            TestContext.Current.CancellationToken);
    }

    private async Task SetAreaActiveAsync(int id, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Areas SET IsActive = {isActive} WHERE Id = {id}",
            TestContext.Current.CancellationToken);
    }

    private static async Task<string> ReadPrimaryCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        return body.GetProperty("errors")[0].GetProperty("code").GetString()!;
    }

    private sealed record LocationResponse(int Id, string NameAr, string NameEn);
}
