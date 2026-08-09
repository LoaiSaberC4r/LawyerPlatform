using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests;

public sealed class LawyerSpecializationRulesTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync()
        => await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task EmptySelectionLeavesSectionIncompleteAndBlocksApprovalSubmission()
    {
        using var client = CreateClient();
        await RegisterAndAuthenticateLawyerAsync(client, "lawyer.empty.specializations");
        var profile = await GetProfileAsync(client);

        var replace = await client.PutAsJsonAsync("/api/v1/lawyer/specializations", new
        {
            specializationIds = Array.Empty<int>(),
            rowVersion = profile.GetProperty("rowVersion").GetString()
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        profile = await GetProfileAsync(client);
        var completion = profile.GetProperty("completion");
        Assert.False(completion.GetProperty("specializationsComplete").GetBoolean());
        Assert.Contains(
            completion.GetProperty("missingRequirements").EnumerateArray(),
            item => item.GetString() == "LegalSpecializations");

        using var submit = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lawyer/submit-for-approval");
        submit.Headers.TryAddWithoutValidation("If-Match", profile.GetProperty("rowVersion").GetString());
        var response = await client.SendAsync(submit, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("Lawyer.ProfileIncomplete", await ReadPrimaryCodeAsync(response));
    }

    [Fact]
    public async Task MultipleActiveSpecializationsHaveNoMaximumWhileDuplicatesAndInactiveIdsAreRejected()
    {
        using var client = CreateClient();
        await RegisterAndAuthenticateLawyerAsync(client, "lawyer.specialization.rules");
        var profile = await GetProfileAsync(client);
        var allApprovedIds = SeedCatalog.LegalSpecializations.Select(seed => seed.Id).ToArray();

        var allActive = await client.PutAsJsonAsync("/api/v1/lawyer/specializations", new
        {
            specializationIds = allApprovedIds,
            rowVersion = profile.GetProperty("rowVersion").GetString()
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, allActive.StatusCode);
        var allActiveBody = await allActive.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(SeedCatalog.ExpectedLegalSpecializationCount, allActiveBody.GetProperty("specializations").GetArrayLength());

        profile = await GetProfileAsync(client);
        var duplicate = await client.PutAsJsonAsync("/api/v1/lawyer/specializations", new
        {
            specializationIds = new[] { allApprovedIds[0], allApprovedIds[0] },
            rowVersion = profile.GetProperty("rowVersion").GetString()
        }, TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)422, duplicate.StatusCode);
        Assert.Equal("Validation.Lawyer.DuplicateSpecialization", await ReadPrimaryCodeAsync(duplicate));

        var inactiveId = allApprovedIds[^1];
        await SetLegalSpecializationActiveAsync(inactiveId, isActive: false);
        var inactive = await client.PutAsJsonAsync("/api/v1/lawyer/specializations", new
        {
            specializationIds = new[] { inactiveId },
            rowVersion = profile.GetProperty("rowVersion").GetString()
        }, TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)422, inactive.StatusCode);
        Assert.Equal("LegalSpecialization.Inactive", await ReadPrimaryCodeAsync(inactive));
    }

    private HttpClient CreateClient()
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task RegisterAndAuthenticateLawyerAsync(HttpClient client, string userName)
    {
        const string password = "LawyerPassword1";
        var register = await client.PostAsJsonAsync("/api/v1/auth/lawyers/register", new
        {
            fullName = "Specialization Test Lawyer",
            userName,
            email = $"{userName}@example.test",
            phoneNumber = userName.Contains("empty", StringComparison.Ordinal)
                ? "01077777771"
                : "01077777772",
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = userName,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString());
    }

    private static async Task<JsonElement> GetProfileAsync(HttpClient client)
    {
        var response = await client.GetAsync(
            "/api/v1/lawyer/profile",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private async Task SetLegalSpecializationActiveAsync(int id, bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE LegalSpecializations SET IsActive = {isActive} WHERE Id = {id}",
            TestContext.Current.CancellationToken);
    }

    private static async Task<string> ReadPrimaryCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("errors")[0].GetProperty("code").GetString()!;
    }
}
