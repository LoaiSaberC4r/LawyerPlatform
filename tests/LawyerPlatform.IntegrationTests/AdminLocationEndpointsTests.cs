using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests;

public sealed class AdminLocationEndpointsTests
{
    [Fact]
    public async Task SuperAdminCrudEnforcesHierarchyScopedUniquenessConcurrencyAndExplicitStatus()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/admin/governorates", TestContext.Current.CancellationToken)).StatusCode);
        await RegisterAndLoginClientAsync(client);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/admin/cities", TestContext.Current.CancellationToken)).StatusCode);
        var adminToken = await LoginAndChangeAdminPasswordAsync(client);
        SetToken(client, adminToken);

        var firstGovernorate = await CreateAsync(client, "/api/v1/admin/governorates", new
        {
            nameAr = "  محافظة الاختبار الأولى  ", nameEn = "  First Test Governorate  ", displayOrder = 900
        });
        Assert.Equal("محافظة الاختبار الأولى", firstGovernorate.GetProperty("nameAr").GetString());
        Assert.Equal("First Test Governorate", firstGovernorate.GetProperty("nameEn").GetString());
        var firstGovernorateId = firstGovernorate.GetProperty("id").GetInt32();
        var secondGovernorate = await CreateAsync(client, "/api/v1/admin/governorates", new
        {
            nameAr = "محافظة الاختبار الثانية", nameEn = "Second Test Governorate", displayOrder = 901
        });
        var secondGovernorateId = secondGovernorate.GetProperty("id").GetInt32();

        await AssertErrorAsync(await client.PostAsJsonAsync("/api/v1/admin/governorates", new
        {
            nameAr = "محافظة الاختبار الأولى", nameEn = "Different English", displayOrder = 902
        }, TestContext.Current.CancellationToken), HttpStatusCode.Conflict, "Governorate.DuplicateNameAr");
        await AssertErrorAsync(await client.PostAsJsonAsync("/api/v1/admin/governorates", new
        {
            nameAr = "اسم عربي مختلف", nameEn = "first test governorate", displayOrder = 902
        }, TestContext.Current.CancellationToken), HttpStatusCode.Conflict, "Governorate.DuplicateNameEn");

        var updatedGovernorateResponse = await client.PutAsJsonAsync(
            $"/api/v1/admin/governorates/{firstGovernorateId}", new
            {
                nameAr = "محافظة الاختبار المحدثة", nameEn = "Updated Test Governorate", displayOrder = 800,
                rowVersion = firstGovernorate.GetProperty("rowVersion").GetString()
            }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, updatedGovernorateResponse.StatusCode);
        var updatedGovernorate = await ReadAsync(updatedGovernorateResponse);
        await AssertErrorAsync(await client.PutAsJsonAsync(
            $"/api/v1/admin/governorates/{firstGovernorateId}", new
            {
                nameAr = "Stale Arabic", nameEn = "Stale English", displayOrder = 1,
                rowVersion = firstGovernorate.GetProperty("rowVersion").GetString()
            }, TestContext.Current.CancellationToken), HttpStatusCode.Conflict, "Governorate.ConcurrencyConflict");

        var deactivatedGovernorate = await ChangeStatusAsync(
            client, "governorates", firstGovernorateId, false, updatedGovernorate.GetProperty("rowVersion").GetString()!);
        Assert.False(deactivatedGovernorate.GetProperty("isActive").GetBoolean());
        var reactivatedGovernorate = await ChangeStatusAsync(
            client, "governorates", firstGovernorateId, true, deactivatedGovernorate.GetProperty("rowVersion").GetString()!);
        Assert.True(reactivatedGovernorate.GetProperty("isActive").GetBoolean());

        var firstCity = await CreateAsync(client, "/api/v1/admin/cities", new
        {
            governorateId = firstGovernorateId, nameAr = "مدينة النطاق", nameEn = "Scoped City", displayOrder = 1
        });
        var firstCityId = firstCity.GetProperty("id").GetInt32();
        var destinationCity = await CreateAsync(client, "/api/v1/admin/cities", new
        {
            governorateId = secondGovernorateId, nameAr = "مدينة النطاق", nameEn = "Scoped City", displayOrder = 1
        });
        var destinationCityId = destinationCity.GetProperty("id").GetInt32();
        await AssertErrorAsync(await client.PostAsJsonAsync("/api/v1/admin/cities", new
        {
            governorateId = 999999, nameAr = "مدينة مفقودة", nameEn = "Missing Parent City", displayOrder = 1
        }, TestContext.Current.CancellationToken), (HttpStatusCode)422, "City.InvalidGovernorate");
        await AssertErrorAsync(await client.PostAsJsonAsync("/api/v1/admin/cities", new
        {
            governorateId = firstGovernorateId, nameAr = "مدينة النطاق", nameEn = "Another City", displayOrder = 2
        }, TestContext.Current.CancellationToken), HttpStatusCode.Conflict, "City.DuplicateNameAr");
        await AssertErrorAsync(await client.PutAsJsonAsync($"/api/v1/admin/cities/{firstCityId}", new
        {
            governorateId = secondGovernorateId, nameAr = "مدينة النطاق", nameEn = "Scoped City", displayOrder = 3,
            rowVersion = firstCity.GetProperty("rowVersion").GetString()
        }, TestContext.Current.CancellationToken), HttpStatusCode.Conflict, "City.DuplicateNameAr");

        var movedCityResponse = await client.PutAsJsonAsync($"/api/v1/admin/cities/{firstCityId}", new
        {
            governorateId = secondGovernorateId, nameAr = "مدينة منقولة", nameEn = "Moved City", displayOrder = 3,
            rowVersion = firstCity.GetProperty("rowVersion").GetString()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, movedCityResponse.StatusCode);
        var movedCity = await ReadAsync(movedCityResponse);
        Assert.Equal(secondGovernorateId, movedCity.GetProperty("governorateId").GetInt32());
        var deactivatedCity = await ChangeStatusAsync(
            client, "cities", firstCityId, false, movedCity.GetProperty("rowVersion").GetString()!);
        Assert.False(deactivatedCity.GetProperty("isActive").GetBoolean());
        var reactivatedCity = await ChangeStatusAsync(
            client, "cities", firstCityId, true, deactivatedCity.GetProperty("rowVersion").GetString()!);

        var firstArea = await CreateAsync(client, "/api/v1/admin/areas", new
        {
            cityId = firstCityId, nameAr = "منطقة النطاق", nameEn = "Scoped Area", displayOrder = 1
        });
        var firstAreaId = firstArea.GetProperty("id").GetInt32();
        await CreateAsync(client, "/api/v1/admin/areas", new
        {
            cityId = destinationCityId, nameAr = "منطقة النطاق", nameEn = "Scoped Area", displayOrder = 1
        });
        await AssertErrorAsync(await client.PostAsJsonAsync("/api/v1/admin/areas", new
        {
            cityId = 999999, nameAr = "منطقة مفقودة", nameEn = "Missing Parent Area", displayOrder = 1
        }, TestContext.Current.CancellationToken), (HttpStatusCode)422, "Area.InvalidCity");
        await AssertErrorAsync(await client.PostAsJsonAsync("/api/v1/admin/areas", new
        {
            cityId = firstCityId, nameAr = "منطقة أخرى", nameEn = "scoped area", displayOrder = 2
        }, TestContext.Current.CancellationToken), HttpStatusCode.Conflict, "Area.DuplicateNameEn");
        await AssertErrorAsync(await client.PutAsJsonAsync($"/api/v1/admin/areas/{firstAreaId}", new
        {
            cityId = destinationCityId, nameAr = "منطقة النطاق", nameEn = "Scoped Area", displayOrder = 2,
            rowVersion = firstArea.GetProperty("rowVersion").GetString()
        }, TestContext.Current.CancellationToken), HttpStatusCode.Conflict, "Area.DuplicateNameAr");

        var movedAreaResponse = await client.PutAsJsonAsync($"/api/v1/admin/areas/{firstAreaId}", new
        {
            cityId = destinationCityId, nameAr = "منطقة منقولة", nameEn = "Moved Area", displayOrder = 2,
            rowVersion = firstArea.GetProperty("rowVersion").GetString()
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, movedAreaResponse.StatusCode);
        var movedArea = await ReadAsync(movedAreaResponse);
        Assert.Equal(secondGovernorateId, movedArea.GetProperty("governorateId").GetInt32());
        Assert.Equal(destinationCityId, movedArea.GetProperty("cityId").GetInt32());
        var deactivatedArea = await ChangeStatusAsync(
            client, "areas", firstAreaId, false, movedArea.GetProperty("rowVersion").GetString()!);
        Assert.False(deactivatedArea.GetProperty("isActive").GetBoolean());
        var reactivatedArea = await ChangeStatusAsync(
            client, "areas", firstAreaId, true, deactivatedArea.GetProperty("rowVersion").GetString()!);
        Assert.True(reactivatedArea.GetProperty("isActive").GetBoolean());

        var cityList = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/cities?governorateId={secondGovernorateId}&searchText=Moved&isActive=true",
            TestContext.Current.CancellationToken);
        Assert.Contains(cityList.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == firstCityId);
        var areaList = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/areas?governorateId={secondGovernorateId}&cityId={destinationCityId}",
            TestContext.Current.CancellationToken);
        Assert.Contains(areaList.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == firstAreaId);
        Assert.Equal("Second Test Governorate", reactivatedCity.GetProperty("governorateNameEn").GetString());
    }

    [Fact]
    public async Task EffectiveHierarchyControlsPublicReferenceDataAndLawyerEligibilityWithoutMutatingChildren()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var adminToken = await LoginAndChangeAdminPasswordAsync(client);
        SetToken(client, adminToken);
        var governorate = await CreateAsync(client, "/api/v1/admin/governorates", new
        {
            nameAr = "محافظة التسلسل", nameEn = "Hierarchy Governorate", displayOrder = 950
        });
        var governorateId = governorate.GetProperty("id").GetInt32();
        var city = await CreateAsync(client, "/api/v1/admin/cities", new
        {
            governorateId, nameAr = "مدينة التسلسل", nameEn = "Hierarchy City", displayOrder = 1
        });
        var cityId = city.GetProperty("id").GetInt32();
        var area = await CreateAsync(client, "/api/v1/admin/areas", new
        {
            cityId, nameAr = "منطقة التسلسل", nameEn = "Hierarchy Area", displayOrder = 1
        });
        var areaId = area.GetProperty("id").GetInt32();
        var lawyerId = await CreateEligibleLawyerAsync(factory, governorateId, cityId, areaId);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicGovernorateContainsAsync(client, governorateId));
        Assert.True(await PublicCityContainsAsync(client, governorateId, cityId));
        Assert.True(await PublicAreaContainsAsync(client, cityId, areaId));
        Assert.True(await PublicLawyerContainsAsync(client, lawyerId));

        SetToken(client, adminToken);
        var inactiveGovernorate = await ChangeStatusAsync(
            client, "governorates", governorateId, false, governorate.GetProperty("rowVersion").GetString()!);
        var childCity = await GetAsync(client, $"/api/v1/admin/cities/{cityId}");
        var childArea = await GetAsync(client, $"/api/v1/admin/areas/{areaId}");
        Assert.True(childCity.GetProperty("isActive").GetBoolean());
        Assert.True(childArea.GetProperty("isActive").GetBoolean());
        client.DefaultRequestHeaders.Authorization = null;
        Assert.False(await PublicGovernorateContainsAsync(client, governorateId));
        Assert.False(await PublicLawyerContainsAsync(client, lawyerId));

        SetToken(client, adminToken);
        var activeGovernorate = await ChangeStatusAsync(
            client, "governorates", governorateId, true, inactiveGovernorate.GetProperty("rowVersion").GetString()!);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicCityContainsAsync(client, governorateId, cityId));
        Assert.True(await PublicAreaContainsAsync(client, cityId, areaId));
        Assert.True(await PublicLawyerContainsAsync(client, lawyerId));

        SetToken(client, adminToken);
        var inactiveCity = await ChangeStatusAsync(
            client, "cities", cityId, false, childCity.GetProperty("rowVersion").GetString()!);
        var stillActiveArea = await GetAsync(client, $"/api/v1/admin/areas/{areaId}");
        Assert.True(stillActiveArea.GetProperty("isActive").GetBoolean());
        client.DefaultRequestHeaders.Authorization = null;
        Assert.False(await PublicAreaContainsAsync(client, cityId, areaId));
        Assert.False(await PublicLawyerContainsAsync(client, lawyerId));

        SetToken(client, adminToken);
        var activeCity = await ChangeStatusAsync(
            client, "cities", cityId, true, inactiveCity.GetProperty("rowVersion").GetString()!);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicAreaContainsAsync(client, cityId, areaId));
        Assert.True(await PublicLawyerContainsAsync(client, lawyerId));

        SetToken(client, adminToken);
        var inactiveArea = await ChangeStatusAsync(
            client, "areas", areaId, false, stillActiveArea.GetProperty("rowVersion").GetString()!);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.False(await PublicAreaContainsAsync(client, cityId, areaId));
        Assert.False(await PublicLawyerContainsAsync(client, lawyerId));

        SetToken(client, adminToken);
        await ChangeStatusAsync(client, "areas", areaId, true, inactiveArea.GetProperty("rowVersion").GetString()!);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicAreaContainsAsync(client, cityId, areaId));
        Assert.True(await PublicLawyerContainsAsync(client, lawyerId));
        _ = activeGovernorate;
        _ = activeCity;
    }

    private static async Task<CustomWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new CustomWebApplicationFactory();
        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        return factory;
    }

    private static HttpClient CreateClient(CustomWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static async Task<JsonElement> CreateAsync(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<JsonElement> ChangeStatusAsync(
        HttpClient client, string resource, int id, bool activate, string rowVersion)
    {
        var action = activate ? "activate" : "deactivate";
        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/{resource}/{id}/{action}", new { rowVersion }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), error => error.GetProperty("code").GetString() == code);
    }

    private static void SetToken(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task RegisterAndLoginClientAsync(HttpClient client)
    {
        const string password = "ClientPassword1";
        var response = await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName = "Location Client", userName = "location.client",
            email = "location.client@example.test", phoneNumber = "01089111111", password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        SetToken(client, await LoginAsync(client, "location.client", password));
    }

    private static async Task<string> LoginAndChangeAdminPasswordAsync(HttpClient client)
    {
        var token = await LoginAsync(client, "superadmin", "InitialPassword1");
        SetToken(client, token);
        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "InitialPassword1", newPassword = "ChangedAdminPassword2"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = userName, password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("accessToken").GetString()!;
    }

    private static async Task<bool> PublicGovernorateContainsAsync(HttpClient client, int id)
    {
        var body = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/public/governorates", TestContext.Current.CancellationToken);
        return body.EnumerateArray().Any(item => item.GetProperty("id").GetInt32() == id);
    }

    private static async Task<bool> PublicCityContainsAsync(HttpClient client, int governorateId, int id)
    {
        var response = await client.GetAsync(
            $"/api/v1/public/governorates/{governorateId}/cities", TestContext.Current.CancellationToken);
        if (response.StatusCode != HttpStatusCode.OK) return false;
        return (await ReadAsync(response)).EnumerateArray().Any(item => item.GetProperty("id").GetInt32() == id);
    }

    private static async Task<bool> PublicAreaContainsAsync(HttpClient client, int cityId, int id)
    {
        var response = await client.GetAsync(
            $"/api/v1/public/cities/{cityId}/areas", TestContext.Current.CancellationToken);
        if (response.StatusCode != HttpStatusCode.OK) return false;
        return (await ReadAsync(response)).EnumerateArray().Any(item => item.GetProperty("id").GetInt32() == id);
    }

    private static async Task<bool> PublicLawyerContainsAsync(HttpClient client, Guid lawyerId)
    {
        var body = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/public/lawyers?pageNumber=1&pageSize=100", TestContext.Current.CancellationToken);
        return body.GetProperty("items").EnumerateArray().Any(item => item.GetProperty("id").GetGuid() == lawyerId);
    }

    private static async Task<Guid> CreateEligibleLawyerAsync(
        CustomWebApplicationFactory factory, int governorateId, int cityId, int areaId)
    {
        var nowUtc = DateTime.UtcNow;
        var account = UserAccount.CreateLawyer(
            "hierarchy.lawyer", "HIERARCHY.LAWYER",
            "hierarchy.lawyer@example.test", "HIERARCHY.LAWYER@EXAMPLE.TEST",
            "01089999999", "hash", nowUtc).Value;
        var profile = LawyerProfile.Create(account, "Hierarchy Lawyer").Value;
        profile.UpdateProfessionalProfile("Hierarchy Lawyer", "Attorney", "Biography", 10, "REG-HIERARCHY");
        profile.UpsertPrimaryOffice(governorateId, cityId, areaId, "Hierarchy office", null);
        profile.ReplaceSpecializations([1]);
        profile.AddDocument("IdentityVerification", "docs/hierarchy-id.pdf", "id.pdf", "application/pdf", 100, nowUtc);
        profile.AddDocument("ProfessionalMembership", "docs/hierarchy-member.pdf", "member.pdf", "application/pdf", 100, nowUtc);
        profile.SubmitForApproval(account.Id, true, true, nowUtc.AddMinutes(1));
        profile.Approve(Guid.Parse("11111111-1111-1111-1111-111111111111"), true, nowUtc.AddMinutes(2));
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        context.LawyerProfiles.Add(profile);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return profile.Id;
    }
}
