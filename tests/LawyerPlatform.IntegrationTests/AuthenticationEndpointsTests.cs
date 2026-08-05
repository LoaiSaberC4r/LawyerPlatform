using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests;

public sealed class AuthenticationEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Swagger_ContainsIdentityAndReferenceDataEndpoints()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var paths = document.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/auth/clients/register", out _));
        Assert.True(paths.TryGetProperty("/api/v1/auth/lawyers/register", out _));
        Assert.True(paths.TryGetProperty("/api/v1/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/v1/auth/change-password", out _));
        Assert.True(paths.TryGetProperty("/api/v1/public/governorates", out _));
        Assert.True(paths.TryGetProperty("/api/v1/public/governorates/{governorateId}/cities", out _));
        Assert.True(paths.TryGetProperty("/api/v1/public/cities/{cityId}/areas", out _));
        Assert.True(paths.TryGetProperty("/api/v1/public/legal-specializations", out _));

        string[] lawyerOnboardingPaths =
        [
            "/api/v1/lawyer/profile",
            "/api/v1/lawyer/profile/image",
            "/api/v1/lawyer/office",
            "/api/v1/lawyer/specializations",
            "/api/v1/lawyer/documents",
            "/api/v1/lawyer/documents/{documentId}/content",
            "/api/v1/lawyer/approval-status",
            "/api/v1/lawyer/submit-for-approval",
            "/api/v1/admin/lawyers",
            "/api/v1/admin/lawyers/{lawyerId}",
            "/api/v1/admin/lawyers/{lawyerId}/profile-image",
            "/api/v1/admin/lawyers/{lawyerId}/documents/{documentId}/content",
            "/api/v1/admin/lawyers/{lawyerId}/approve",
            "/api/v1/admin/lawyers/{lawyerId}/reject",
            "/api/v1/admin/lawyers/{lawyerId}/request-changes",
            "/api/v1/admin/lawyers/{lawyerId}/suspend",
            "/api/v1/admin/lawyers/{lawyerId}/reactivate",
            "/api/v1/public/lawyers",
            "/api/v1/public/lawyers/{lawyerId}",
            "/api/v1/public/lawyers/{lawyerId}/profile-image"
        ];
        foreach (var path in lawyerOnboardingPaths)
        {
            Assert.True(paths.TryGetProperty(path, out _), $"Swagger path '{path}' is missing.");
        }
    }

    [Fact]
    public async Task IdentityFoundation_EndToEndScenario_Works()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var cancellationToken = TestContext.Current.CancellationToken;

        var clientRegistration = await client.PostAsJsonAsync(
            "/api/v1/auth/clients/register",
            new
            {
                fullName = "Client One",
                userName = "client.one",
                email = "client.one@example.test",
                phoneNumber = "01000000001",
                password = "ClientPassword1"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, clientRegistration.StatusCode);

        var lawyerRegistration = await client.PostAsJsonAsync(
            "/api/v1/auth/lawyers/register",
            new
            {
                fullName = "Lawyer One",
                userName = "lawyer.one",
                email = "lawyer.one@example.test",
                phoneNumber = "01000000002",
                password = "LawyerPassword1"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Created, lawyerRegistration.StatusCode);
        var lawyerBody = await lawyerRegistration.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("Draft", lawyerBody.GetProperty("approvalStatus").GetString());

        var duplicateRegistration = await client.PostAsJsonAsync(
            "/api/v1/auth/clients/register",
            new
            {
                fullName = "Duplicate",
                userName = "CLIENT.ONE",
                email = "other@example.test",
                phoneNumber = "01000000003",
                password = "ClientPassword1"
            },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, duplicateRegistration.StatusCode);

        var userNameLogin = await LoginAsync(client, "client.one", "ClientPassword1", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, userNameLogin.StatusCode);
        var emailLogin = await LoginAsync(client, "CLIENT.ONE@EXAMPLE.TEST", "ClientPassword1", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, emailLogin.StatusCode);

        var invalidLogin = await LoginAsync(client, "missing.user", "WrongPassword1", cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidLogin.StatusCode);

        await VerifySuperAdminSeederIsIdempotentAsync(cancellationToken);

        var superAdminLogin = await LoginAsync(client, "superadmin", "InitialPassword1", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, superAdminLogin.StatusCode);
        var restrictedToken = await ReadTokenAsync(superAdminLogin, cancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", restrictedToken);
        var restrictedResponse = await client.DeleteAsync(
            $"/api/v1/catalog-items/{Guid.NewGuid()}/permanent",
            cancellationToken);
        Assert.True(
            restrictedResponse.StatusCode == HttpStatusCode.Forbidden,
            $"Expected restricted token to be forbidden, received {restrictedResponse.StatusCode}. Authenticate: {string.Join(", ", restrictedResponse.Headers.WwwAuthenticate)}");

        var changePassword = await client.PostAsJsonAsync(
            "/api/v1/auth/change-password",
            new { currentPassword = "InitialPassword1", newPassword = "ChangedPassword2" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, changePassword.StatusCode);
        var unrestrictedToken = await ReadTokenAsync(changePassword, cancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unrestrictedToken);
        var unrestrictedResponse = await client.DeleteAsync(
            $"/api/v1/catalog-items/{Guid.NewGuid()}/permanent",
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unrestrictedResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var changedPasswordLogin = await LoginAsync(
            client,
            "admin@lawyerplatform.test",
            "ChangedPassword2",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, changedPasswordLogin.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/public/governorates", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/public/legal-specializations", cancellationToken)).StatusCode);
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string userNameOrEmail,
        string password,
        CancellationToken cancellationToken)
        => client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { userNameOrEmail, password },
            cancellationToken);

    private static async Task<string> ReadTokenAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("accessToken").GetString()!;
    }

    private async Task VerifySuperAdminSeederIsIdempotentAsync(CancellationToken cancellationToken)
    {
        await factory.SeedDatabaseAsync(cancellationToken);

        SuperAdminSnapshot before;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            before = await scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>()
                .UserAccounts
                .AsNoTracking()
                .Where(account => account.Role == AccountRole.SuperAdmin)
                .Select(account => new SuperAdminSnapshot(account.PasswordHash, account.PasswordChangedOnUtc, account.IsFirstLogin))
                .SingleAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<IEnsureSeeding>().SeedDatabaseAsync(cancellationToken);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var accounts = await verificationScope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>()
            .UserAccounts
            .AsNoTracking()
            .Where(account => account.Role == AccountRole.SuperAdmin)
            .Select(account => new SuperAdminSnapshot(account.PasswordHash, account.PasswordChangedOnUtc, account.IsFirstLogin))
            .ToListAsync(cancellationToken);

        Assert.Single(accounts);
        Assert.Equal(before, accounts[0]);
    }

    private sealed record SuperAdminSnapshot(string PasswordHash, DateTime PasswordChangedOnUtc, bool IsFirstLogin);
}
