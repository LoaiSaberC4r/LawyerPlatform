using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests;

public sealed class ConsultationRequestCreationEndpointsTests
{
    [Fact]
    public async Task GuestCreationValidatesIdentityLawyerSpecializationAndPreferredTime()
    {
        await using var factory = await CreateFactoryAsync();
        var eligibleLawyerId = await CreateLawyerAsync(factory, "guest.eligible", complete: true, approved: true, accountActive: true);
        var draftLawyerId = await CreateLawyerAsync(factory, "guest.draft", complete: true, approved: false, accountActive: true);
        var inactiveLawyerId = await CreateLawyerAsync(factory, "guest.inactive", complete: true, approved: true, accountActive: false);
        var incompleteLawyerId = await CreateLawyerAsync(factory, "guest.incomplete", complete: false, approved: true, accountActive: true);
        using var client = CreateClient(factory);

        var success = await PostGuestAsync(client, eligibleLawyerId, specializationId: null);
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        var created = await success.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal("New", created.GetProperty("status").GetString());
        Assert.StartsWith("CR-", created.GetProperty("referenceNumber").GetString(), StringComparison.Ordinal);

        var missingName = await client.PostAsJsonAsync("/api/v1/public/consultation-requests", new
        {
            lawyerId = eligibleLawyerId,
            fullName = "",
            phoneNumber = "01012345678",
            description = "Description"
        }, TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)422, missingName.StatusCode);

        var missingPhone = await client.PostAsJsonAsync("/api/v1/public/consultation-requests", new
        {
            lawyerId = eligibleLawyerId,
            fullName = "Guest",
            phoneNumber = "",
            description = "Description"
        }, TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)422, missingPhone.StatusCode);

        var invalidEmail = await client.PostAsJsonAsync("/api/v1/public/consultation-requests", new
        {
            lawyerId = eligibleLawyerId,
            fullName = "Guest",
            phoneNumber = "01012345678",
            email = "invalid-email",
            description = "Description"
        }, TestContext.Current.CancellationToken);
        Assert.Equal((HttpStatusCode)422, invalidEmail.StatusCode);

        await AssertErrorAsync(
            await PostGuestAsync(client, Guid.NewGuid(), null),
            HttpStatusCode.UnprocessableEntity,
            "ConsultationRequest.LawyerUnavailable");
        await AssertErrorAsync(
            await PostGuestAsync(client, draftLawyerId, null),
            HttpStatusCode.UnprocessableEntity,
            "ConsultationRequest.LawyerUnavailable");
        await AssertErrorAsync(
            await PostGuestAsync(client, inactiveLawyerId, null),
            HttpStatusCode.UnprocessableEntity,
            "ConsultationRequest.LawyerUnavailable");
        await AssertErrorAsync(
            await PostGuestAsync(client, incompleteLawyerId, null),
            HttpStatusCode.UnprocessableEntity,
            "ConsultationRequest.LawyerUnavailable");
        await AssertErrorAsync(
            await PostGuestAsync(client, eligibleLawyerId, 999999),
            HttpStatusCode.NotFound,
            "LegalSpecialization.NotFound");
        await AssertErrorAsync(
            await PostGuestAsync(client, eligibleLawyerId, 2),
            HttpStatusCode.UnprocessableEntity,
            "ConsultationRequest.SpecializationNotOfferedByLawyer");

        await SetSpecializationActiveAsync(factory, 1, isActive: false);
        await AssertErrorAsync(
            await PostGuestAsync(client, eligibleLawyerId, 1),
            HttpStatusCode.UnprocessableEntity,
            "LegalSpecialization.Inactive");
        await SetSpecializationActiveAsync(factory, 1, isActive: true);

        var past = await client.PostAsJsonAsync("/api/v1/public/consultation-requests", new
        {
            lawyerId = eligibleLawyerId,
            fullName = "Guest",
            phoneNumber = "01012345678",
            description = "Description",
            preferredAppointmentOnUtc = DateTime.UtcNow.AddMinutes(-1)
        }, TestContext.Current.CancellationToken);
        await AssertErrorAsync(
            past,
            HttpStatusCode.UnprocessableEntity,
            "ConsultationRequest.PreferredAppointmentMustBeFuture");
    }

    [Fact]
    public async Task AuthenticatedClientCreationUsesCurrentProfileAndCannotBeImpersonated()
    {
        await using var factory = await CreateFactoryAsync();
        var lawyerId = await CreateLawyerAsync(factory, "client.target", complete: true, approved: true, accountActive: true);
        using var client = CreateClient(factory);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/client/consultation-requests", new
            {
                lawyerId,
                description = "Anonymous attempt"
            }, TestContext.Current.CancellationToken)).StatusCode);

        var clientProfileId = await RegisterAndLoginClientAsync(client);
        var fakeClientProfileId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/v1/client/consultation-requests", new
        {
            lawyerId,
            legalSpecializationId = 1,
            description = "  Authenticated client request  ",
            preferredAppointmentOnUtc = DateTime.UtcNow.AddDays(1),
            clientProfileId = fakeClientProfileId,
            fullName = "Impersonated Name",
            phoneNumber = "01099999999"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var stored = await context.ConsultationRequests
            .AsNoTracking()
            .SingleAsync(
                item => item.Id == body.GetProperty("id").GetGuid(),
                TestContext.Current.CancellationToken);
        Assert.Equal(clientProfileId, stored.ClientProfileId);
        Assert.NotEqual(fakeClientProfileId, stored.ClientProfileId);
        Assert.Null(stored.GuestFullName);
        Assert.Null(stored.GuestPhoneNumber);
        Assert.Equal("Authenticated client request", stored.Description);
        Assert.Equal(ConsultationRequestStatus.New, stored.Status);
    }

    [Fact]
    public async Task GuestCreationHasDedicatedRateLimit()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var statuses = new List<HttpStatusCode>();
        for (var index = 0; index < 21; index++)
        {
            statuses.Add((await PostGuestAsync(client, Guid.NewGuid(), null)).StatusCode);
        }

        Assert.Equal(20, statuses.Count(status => status == HttpStatusCode.UnprocessableEntity));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
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

    private static Task<HttpResponseMessage> PostGuestAsync(
        HttpClient client,
        Guid lawyerId,
        int? specializationId)
        => client.PostAsJsonAsync("/api/v1/public/consultation-requests", new
        {
            lawyerId,
            legalSpecializationId = specializationId,
            fullName = "Guest Requester",
            phoneNumber = "01012345678",
            email = "guest@example.test",
            description = "Legal consultation description"
        }, TestContext.Current.CancellationToken);

    private static async Task<Guid> RegisterAndLoginClientAsync(HttpClient client)
    {
        const string userName = "consultation.client";
        const string password = "ClientPassword1";
        var register = await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName = "Consultation Client",
            userName,
            email = "consultation.client@example.test",
            phoneNumber = "01085858585",
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registered = await register.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = userName,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            loginBody.GetProperty("accessToken").GetString());
        return registered.GetProperty("clientProfileId").GetGuid();
    }

    private static async Task<Guid> CreateLawyerAsync(
        CustomWebApplicationFactory factory,
        string userName,
        bool complete,
        bool approved,
        bool accountActive)
    {
        var nowUtc = new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);
        var normalized = userName.ToUpperInvariant();
        var account = UserAccount.CreateLawyer(
            userName,
            normalized,
            $"{userName}@example.test",
            $"{normalized}@EXAMPLE.TEST",
            $"0108{Math.Abs(userName.GetHashCode(StringComparison.Ordinal)) % 10_000_000:D7}",
            "hash",
            nowUtc).Value;
        var profile = LawyerProfile.Create(account, $"{userName} Lawyer").Value;
        if (complete)
        {
            profile.UpdateProfessionalProfile($"{userName} Lawyer", "Attorney", "Biography", 8, $"REG-{normalized}");
            var area = EgyptLocationSeedCatalog.Areas[0];
            var city = EgyptLocationSeedCatalog.Cities.Single(item => item.Id == area.CityId);
            profile.UpsertPrimaryOffice(city.GovernorateId, city.Id, area.Id, "Complete address", null);
            profile.ReplaceSpecializations([1]);
            profile.AddDocument("IdentityVerification", $"docs/{userName}-id.pdf", "id.pdf", "application/pdf", 100, nowUtc);
            profile.AddDocument("ProfessionalMembership", $"docs/{userName}-member.pdf", "member.pdf", "application/pdf", 100, nowUtc);
        }

        if (approved)
        {
            profile.SubmitForApproval(account.Id, true, true, nowUtc.AddMinutes(1));
            profile.Approve(Guid.Parse("11111111-1111-1111-1111-111111111111"), true, nowUtc.AddMinutes(2));
        }

        if (!accountActive)
        {
            account.Deactivate(nowUtc.AddMinutes(3));
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        context.LawyerProfiles.Add(profile);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return profile.Id;
    }

    private static async Task SetSpecializationActiveAsync(
        CustomWebApplicationFactory factory,
        int id,
        bool isActive)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE LegalSpecializations SET IsActive = {isActive} WHERE Id = {id}",
            TestContext.Current.CancellationToken);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Contains(
            body.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == expectedCode);
    }
}
