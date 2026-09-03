using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests;

public sealed class AdminLegalSpecializationEndpointsTests
{
    [Fact]
    public async Task EndpointsRequireSuperAdmin()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync(
                "/api/v1/admin/legal-specializations",
                TestContext.Current.CancellationToken)).StatusCode);

        await RegisterClientAsync(client);
        var token = await LoginAsync(client, "specialization.client", "ClientPassword1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync(
                "/api/v1/admin/legal-specializations",
                TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task SuperAdminCanCreateUpdateFilterDeactivateAndActivateWithStableConflicts()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAndChangeAdminPasswordAsync(client));

        var create = await client.PostAsJsonAsync("/api/v1/admin/legal-specializations", new
        {
            nameAr = "  قانون الطاقة  ",
            nameEn = "  Energy Law  ",
            displayOrder = 190
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var id = created.GetProperty("id").GetInt32();
        Assert.True(id >= 10_000);
        var originalRowVersion = created.GetProperty("rowVersion").GetString()!;
        Assert.Equal("قانون الطاقة", created.GetProperty("nameAr").GetString());
        Assert.Equal("Energy Law", created.GetProperty("nameEn").GetString());

        var selfExcludedUpdate = await client.PutAsJsonAsync(
            $"/api/v1/admin/legal-specializations/{id}",
            new
            {
                nameAr = "قانون الطاقة",
                nameEn = "Energy Law",
                displayOrder = 191,
                rowVersion = originalRowVersion
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, selfExcludedUpdate.StatusCode);
        var selfUpdated = await selfExcludedUpdate.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);
        originalRowVersion = selfUpdated.GetProperty("rowVersion").GetString()!;

        var duplicateArabic = await client.PostAsJsonAsync("/api/v1/admin/legal-specializations", new
        {
            nameAr = "قانون الطاقة",
            nameEn = "Different English",
            displayOrder = 200
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateArabic.StatusCode);
        Assert.Equal("LegalSpecialization.DuplicateNameAr", await ReadPrimaryCodeAsync(duplicateArabic));

        var duplicateEnglish = await client.PostAsJsonAsync("/api/v1/admin/legal-specializations", new
        {
            nameAr = "اسم مختلف",
            nameEn = "energy law",
            displayOrder = 200
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateEnglish.StatusCode);
        Assert.Equal("LegalSpecialization.DuplicateNameEn", await ReadPrimaryCodeAsync(duplicateEnglish));

        var update = await client.PutAsJsonAsync($"/api/v1/admin/legal-specializations/{id}", new
        {
            nameAr = "قانون الطاقة والموارد",
            nameEn = "Energy and Resources Law",
            displayOrder = 195,
            rowVersion = originalRowVersion
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var updatedRowVersion = updated.GetProperty("rowVersion").GetString()!;

        var staleUpdate = await client.PutAsJsonAsync($"/api/v1/admin/legal-specializations/{id}", new
        {
            nameAr = "تعديل قديم",
            nameEn = "Stale update",
            displayOrder = 196,
            rowVersion = originalRowVersion
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
        Assert.Equal("LegalSpecialization.ConcurrencyConflict", await ReadPrimaryCodeAsync(staleUpdate));

        var deactivate = await client.PostAsJsonAsync(
            $"/api/v1/admin/legal-specializations/{id}/deactivate",
            new { rowVersion = updatedRowVersion },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        var deactivated = await deactivate.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.False(deactivated.GetProperty("isActive").GetBoolean());

        var publicItems = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/public/legal-specializations",
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(publicItems.EnumerateArray(), item => item.GetProperty("id").GetInt32() == id);

        var inactiveList = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/admin/legal-specializations?isActive=false&pageNumber=1&pageSize=100",
            TestContext.Current.CancellationToken);
        Assert.Contains(
            inactiveList.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetInt32() == id);

        var deactivatedRowVersion = deactivated.GetProperty("rowVersion").GetString()!;
        var activate = await client.PostAsJsonAsync(
            $"/api/v1/admin/legal-specializations/{id}/activate",
            new { rowVersion = deactivatedRowVersion },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
    }

    [Fact]
    public async Task DeactivationKeepsHistoricalRelationsAndUsesAtLeastOneActiveForEligibility()
    {
        await using var factory = await CreateFactoryAsync();
        var lawyerId = await CreateApprovedLawyerAsync(factory);
        using var client = CreateClient(factory);
        Assert.True(await PublicSearchContainsAsync(client, lawyerId));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAndChangeAdminPasswordAsync(client));

        var first = await GetSpecializationAsync(client, 1);
        var deactivateFirst = await client.PostAsJsonAsync(
            "/api/v1/admin/legal-specializations/1/deactivate",
            new { rowVersion = first.GetProperty("rowVersion").GetString() },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deactivateFirst.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicSearchContainsAsync(client, lawyerId));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(client, "superadmin", "ChangedAdminPassword2"));
        var second = await GetSpecializationAsync(client, 2);
        var deactivateSecond = await client.PostAsJsonAsync(
            "/api/v1/admin/legal-specializations/2/deactivate",
            new { rowVersion = second.GetProperty("rowVersion").GetString() },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deactivateSecond.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.False(await PublicSearchContainsAsync(client, lawyerId));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            Assert.Equal(
                2,
                await context.LawyerSpecializations.CountAsync(
                    item => item.LawyerProfileId == lawyerId,
                    TestContext.Current.CancellationToken));
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await LoginAsync(client, "superadmin", "ChangedAdminPassword2"));
        var inactiveFirst = await GetSpecializationAsync(client, 1);
        var reactivate = await client.PostAsJsonAsync(
            "/api/v1/admin/legal-specializations/1/activate",
            new { rowVersion = inactiveFirst.GetProperty("rowVersion").GetString() },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.True(await PublicSearchContainsAsync(client, lawyerId));
    }

    private static async Task<CustomWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new CustomWebApplicationFactory();
        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        return factory;
    }

    private static HttpClient CreateClient(CustomWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task RegisterClientAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName = "Specialization Client",
            userName = "specialization.client",
            email = "specialization.client@example.test",
            phoneNumber = "01081818181",
            password = "ClientPassword1"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<string> LoginAndChangeAdminPasswordAsync(HttpClient client)
    {
        var token = await LoginAsync(client, "superadmin", "InitialPassword1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "InitialPassword1",
            newPassword = "ChangedAdminPassword2"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> LoginAsync(HttpClient client, string identifier, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = identifier,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> GetSpecializationAsync(HttpClient client, int id)
    {
        var response = await client.GetAsync(
            $"/api/v1/admin/legal-specializations/{id}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> CreateApprovedLawyerAsync(CustomWebApplicationFactory factory)
    {
        var nowUtc = new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);
        var account = UserAccount.CreateLawyer(
            "block2.lawyer",
            "BLOCK2.LAWYER",
            "block2.lawyer@example.test",
            "BLOCK2.LAWYER@EXAMPLE.TEST",
            "01082828282",
            "hash",
            nowUtc).Value;
        var profile = LawyerProfile.Create(account, "Block Two Lawyer").Value;
        profile.UpdateProfessionalProfile("Block Two Lawyer", "Attorney", "Biography", 8, "BLOCK2-REG");
        var area = EgyptLocationSeedCatalog.Areas[0];
        var city = EgyptLocationSeedCatalog.Cities.Single(item => item.Id == area.CityId);
        profile.UpsertPrimaryOffice(city.GovernorateId, city.Id, area.Id, "Complete address", null);
        profile.ReplaceSpecializations([1, 2]);
        profile.AddDocument("IdentityVerification", "docs/id.pdf", "id.pdf", "application/pdf", 100, nowUtc);
        profile.AddDocument("ProfessionalMembership", "docs/member.pdf", "member.pdf", "application/pdf", 100, nowUtc);
        profile.SubmitForApproval(account.Id, true, true, nowUtc.AddMinutes(1));
        profile.Approve(Guid.Parse("11111111-1111-1111-1111-111111111111"), true, nowUtc.AddMinutes(2));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        context.LawyerProfiles.Add(profile);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return profile.Id;
    }

    private static async Task<bool> PublicSearchContainsAsync(HttpClient client, Guid lawyerId)
    {
        var response = await client.GetAsync(
            "/api/v1/public/lawyers?pageNumber=1&pageSize=100",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("items").EnumerateArray()
            .Any(item => item.GetProperty("id").GetGuid() == lawyerId);
    }

    private static async Task<string> ReadPrimaryCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("errors")[0].GetProperty("code").GetString()!;
    }
}
