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

public sealed class ClientAndGuestConsultationReadEndpointsTests
{
    [Fact]
    public async Task ClientsSeeOnlyOwnRequestsWhileGuestsUseGenericPrivateTracking()
    {
        await using var factory = await CreateFactoryAsync();
        var lawyerId = await CreateEligibleLawyerAsync(factory);
        using var client = CreateClient(factory);

        var firstClient = await RegisterAndLoginClientAsync(
            client,
            "history.client.one",
            "history.client.one@example.test",
            "01087111111");
        var firstRequest = await CreateClientRequestAsync(client, lawyerId, "First private description");
        await RejectAsync(factory, firstRequest.Id, "Private client rejection");

        var listResponse = await client.GetAsync(
            "/api/v1/client/consultation-requests?status=Rejected&pageNumber=1&pageSize=20",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        AssertNoPrivateLocationFields(list.GetRawText());
        Assert.Equal(1, list.GetProperty("totalItems").GetInt64());
        Assert.Equal(firstRequest.Id, list.GetProperty("items")[0].GetProperty("id").GetGuid());

        var ownDetails = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/client/consultation-requests/{firstRequest.Id}",
            TestContext.Current.CancellationToken);
        Assert.Equal("First private description", ownDetails.GetProperty("description").GetString());
        Assert.Equal("Private client rejection", ownDetails.GetProperty("rejectionReason").GetString());
        AssertNoPrivateLocationFields(ownDetails.GetRawText());

        await RegisterAndLoginClientAsync(
            client,
            "history.client.two",
            "history.client.two@example.test",
            "01087222222");
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(
                $"/api/v1/client/consultation-requests/{firstRequest.Id}",
                TestContext.Current.CancellationToken)).StatusCode);
        var secondList = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/client/consultation-requests",
            TestContext.Current.CancellationToken);
        Assert.Equal(0, secondList.GetProperty("totalItems").GetInt64());
        AssertNoPrivateLocationFields(secondList.GetRawText());

        client.DefaultRequestHeaders.Authorization = null;
        var guest = await CreateGuestRequestAsync(client, lawyerId);
        await RejectAsync(factory, guest.Id, "Never expose this guest reason");

        var trackedResponse = await client.PostAsJsonAsync(
            "/api/v1/public/consultation-requests/track",
            new { referenceNumber = guest.ReferenceNumber.ToLowerInvariant(), phoneNumber = "01087333333" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, trackedResponse.StatusCode);
        var trackedText = await trackedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var tracked = JsonDocument.Parse(trackedText).RootElement;
        Assert.Equal("Rejected", tracked.GetProperty("status").GetString());
        Assert.False(tracked.TryGetProperty("rejectionReason", out _));
        Assert.False(tracked.TryGetProperty("statusHistory", out _));
        Assert.False(tracked.TryGetProperty("description", out _));
        Assert.DoesNotContain("Never expose this guest reason", trackedText, StringComparison.Ordinal);
        Assert.DoesNotContain("01087333333", trackedText, StringComparison.Ordinal);
        Assert.DoesNotContain(firstClient.ToString(), trackedText, StringComparison.OrdinalIgnoreCase);
        AssertNoPrivateLocationFields(trackedText);

        var wrongReference = await client.PostAsJsonAsync(
            "/api/v1/public/consultation-requests/track",
            new { referenceNumber = "CR-NOT-FOUND", phoneNumber = "01087333333" },
            TestContext.Current.CancellationToken);
        var wrongPhone = await client.PostAsJsonAsync(
            "/api/v1/public/consultation-requests/track",
            new { referenceNumber = guest.ReferenceNumber, phoneNumber = "01087999999" },
            TestContext.Current.CancellationToken);
        await AssertVerificationFailureAsync(wrongReference);
        await AssertVerificationFailureAsync(wrongPhone);

        var updateAttempt = await client.PutAsJsonAsync(
            $"/api/v1/client/consultation-requests/{firstRequest.Id}/status",
            new { status = "Approved" },
            TestContext.Current.CancellationToken);
        Assert.Contains(updateAttempt.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task GuestTrackingHasDedicatedRateLimit()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var statuses = new List<HttpStatusCode>();
        for (var index = 0; index < 21; index++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/public/consultation-requests/track",
                new { referenceNumber = $"CR-MISSING-{index}", phoneNumber = "01087333333" },
                TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(20, statuses.Count(status => status == HttpStatusCode.NotFound));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    private static async Task<CustomWebApplicationFactory> CreateFactoryAsync()
    {
        var factory = new CustomWebApplicationFactory();
        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        return factory;
    }

    private static HttpClient CreateClient(CustomWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static async Task<Guid> RegisterAndLoginClientAsync(
        HttpClient client,
        string userName,
        string email,
        string phoneNumber)
    {
        const string password = "ClientPassword1";
        client.DefaultRequestHeaders.Authorization = null;
        var register = await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName = $"{userName} Full Name",
            userName,
            email,
            phoneNumber,
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
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString());
        return registered.GetProperty("clientProfileId").GetGuid();
    }

    private static async Task<(Guid Id, string ReferenceNumber)> CreateClientRequestAsync(
        HttpClient client,
        Guid lawyerId,
        string description)
    {
        var response = await client.PostAsJsonAsync("/api/v1/client/consultation-requests", new
        {
            lawyerId,
            legalSpecializationId = 1,
            description
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        AssertNoPrivateLocationFields(body.GetRawText());
        return (body.GetProperty("id").GetGuid(), body.GetProperty("referenceNumber").GetString()!);
    }

    private static async Task<(Guid Id, string ReferenceNumber)> CreateGuestRequestAsync(
        HttpClient client,
        Guid lawyerId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/public/consultation-requests", new
        {
            lawyerId,
            legalSpecializationId = 1,
            fullName = "Tracked Guest",
            phoneNumber = "01087333333",
            email = "tracked.guest@example.test",
            description = "Guest secret description"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        AssertNoPrivateLocationFields(body.GetRawText());
        return (body.GetProperty("id").GetGuid(), body.GetProperty("referenceNumber").GetString()!);
    }

    private static async Task RejectAsync(
        CustomWebApplicationFactory factory,
        Guid requestId,
        string reason)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var request = await context.ConsultationRequests.SingleAsync(
            item => item.Id == requestId,
            TestContext.Current.CancellationToken);
        Assert.True(request.ChangeStatus(
            ConsultationRequestStatus.Rejected,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            reason,
            DateTime.UtcNow).IsSuccess);
        context.ConsultationRequestStatusHistory.Add(request.StatusHistory.Last());
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> CreateEligibleLawyerAsync(CustomWebApplicationFactory factory)
    {
        var nowUtc = DateTime.UtcNow;
        var account = UserAccount.CreateLawyer(
            "history.target",
            "HISTORY.TARGET",
            "history.target@example.test",
            "HISTORY.TARGET@EXAMPLE.TEST",
            "01087000000",
            "hash",
            nowUtc).Value;
        var profile = LawyerProfile.Create(account, "History Target Lawyer").Value;
        profile.UpdateProfessionalProfile("History Target Lawyer", "Attorney", "Biography", 9, "REG-HISTORY");
        var area = EgyptLocationSeedCatalog.Areas[0];
        var city = EgyptLocationSeedCatalog.Cities.Single(item => item.Id == area.CityId);
        profile.UpsertPrimaryOffice(city.GovernorateId, city.Id, area.Id, "Complete address", null);
        profile.ReplaceSpecializations([1]);
        profile.AddDocument("IdentityVerification", "docs/history-id.pdf", "id.pdf", "application/pdf", 100, nowUtc);
        profile.AddDocument("ProfessionalMembership", "docs/history-member.pdf", "member.pdf", "application/pdf", 100, nowUtc);
        profile.SubmitForApproval(account.Id, true, true, nowUtc.AddMinutes(1));
        profile.Approve(Guid.Parse("11111111-1111-1111-1111-111111111111"), true, nowUtc.AddMinutes(2));

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        context.LawyerProfiles.Add(profile);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return profile.Id;
    }

    private static async Task AssertVerificationFailureAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Contains(
            body.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() ==
                     "ConsultationRequest.ReferenceVerificationFailed");
    }

    private static void AssertNoPrivateLocationFields(string json)
    {
        Assert.DoesNotContain("latitude", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("longitude", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lawyerOfficeMapUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("googleMapsUrl", json, StringComparison.OrdinalIgnoreCase);
    }
}
