using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Seeding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LawyerPlatform.Application.Notifications.Email;

namespace LawyerPlatform.IntegrationTests;

public sealed class LawyerConsultationRequestEndpointsTests
{
    private const string LawyerPublicPhoneNumber = "01012345678";

    [Fact]
    public async Task LawyerCanOnlyViewAndTransitionOwnRequestsWithContactHistoryAndConcurrency()
    {
        await using var factory = new CustomWebApplicationFactory();
        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var firstLawyer = await RegisterApproveAndLoginLawyerAsync(
            factory, client, "request.lawyer.one", "01086111111", includeCoordinates: true);
        var secondLawyer = await RegisterApproveAndLoginLawyerAsync(
            factory, client, "request.lawyer.two", "01086222222", includeCoordinates: false);

        client.DefaultRequestHeaders.Authorization = null;
        var firstGuest = await CreateGuestRequestAsync(client, firstLawyer.ProfileId, "First Guest", "01011112222");
        var completedCandidate = await CreateGuestRequestAsync(client, firstLawyer.ProfileId, "Second Guest", "01033334444");
        var otherLawyerRequest = await CreateGuestRequestAsync(client, secondLawyer.ProfileId, "Other Guest", "01055556666");
        var clientRequest = await CreateClientRequestAsync(client, firstLawyer.ProfileId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstLawyer.Token);
        var listResponse = await client.GetAsync(
            "/api/v1/lawyer/consultation-requests?pageNumber=1&pageSize=2",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(3, list.GetProperty("totalItems").GetInt64());
        Assert.Equal(2, list.GetProperty("items").GetArrayLength());
        Assert.All(list.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.Equal("Onsite", item.GetProperty("consultationType").GetString());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("consultationPrice").ValueKind);
        });
        Assert.DoesNotContain(
            list.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == otherLawyerRequest.Id);

        var filtered = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/lawyer/consultation-requests?status=New&pageNumber=1&pageSize=100",
            TestContext.Current.CancellationToken);
        Assert.Equal(3, filtered.GetProperty("totalItems").GetInt64());

        var guestDetails = await GetDetailsAsync(client, firstGuest.Id);
        Assert.Equal("Guest", guestDetails.GetProperty("requesterType").GetString());
        Assert.Equal("First Guest", guestDetails.GetProperty("requesterFullName").GetString());
        Assert.Equal("01011112222", guestDetails.GetProperty("requesterPhoneNumber").GetString());
        Assert.Equal("first.guest@example.test", guestDetails.GetProperty("requesterEmail").GetString());
        Assert.Equal("Onsite", guestDetails.GetProperty("consultationType").GetString());
        Assert.Equal(JsonValueKind.Null, guestDetails.GetProperty("consultationPrice").ValueKind);

        var clientDetails = await GetDetailsAsync(client, clientRequest.Id);
        Assert.Equal("Client", clientDetails.GetProperty("requesterType").GetString());
        Assert.Equal("Request Client", clientDetails.GetProperty("requesterFullName").GetString());
        Assert.Equal("01086999999", clientDetails.GetProperty("requesterPhoneNumber").GetString());
        Assert.Equal("request.client@example.test", clientDetails.GetProperty("requesterEmail").GetString());

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(
                $"/api/v1/lawyer/consultation-requests/{otherLawyerRequest.Id}",
                TestContext.Current.CancellationToken)).StatusCode);

        var originalRowVersion = guestDetails.GetProperty("rowVersion").GetString()!;
        var underReview = await UpdateStatusAsync(
            client,
            firstGuest.Id,
            ConsultationRequestStatus.UnderReview,
            null,
            originalRowVersion);
        Assert.Equal(HttpStatusCode.OK, underReview.StatusCode);
        var underReviewBody = await underReview.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var stale = await UpdateStatusAsync(
            client,
            firstGuest.Id,
            ConsultationRequestStatus.Approved,
            null,
            originalRowVersion);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ConsultationRequest.ConcurrencyConflict", await ReadPrimaryCodeAsync(stale));

        var invalid = await UpdateStatusAsync(
            client,
            firstGuest.Id,
            ConsultationRequestStatus.Completed,
            null,
            underReviewBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal((HttpStatusCode)422, invalid.StatusCode);
        Assert.Equal("ConsultationRequest.InvalidStatusTransition", await ReadPrimaryCodeAsync(invalid));

        var noReason = await UpdateStatusAsync(
            client,
            firstGuest.Id,
            ConsultationRequestStatus.Rejected,
            "",
            underReviewBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal((HttpStatusCode)422, noReason.StatusCode);

        var rejected = await UpdateStatusAsync(
            client,
            firstGuest.Id,
            ConsultationRequestStatus.Rejected,
            "  Conflict of interest  ",
            underReviewBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var terminal = await UpdateStatusAsync(
            client,
            firstGuest.Id,
            ConsultationRequestStatus.Approved,
            null,
            rejectedBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal((HttpStatusCode)422, terminal.StatusCode);

        var rejectedDetails = await GetDetailsAsync(client, firstGuest.Id);
        Assert.Equal("Conflict of interest", rejectedDetails.GetProperty("rejectionReason").GetString());
        Assert.Equal(2, rejectedDetails.GetProperty("statusHistory").GetArrayLength());
        Assert.All(
            rejectedDetails.GetProperty("statusHistory").EnumerateArray(),
            history => Assert.Equal(firstLawyer.UserAccountId, history.GetProperty("changedByUserId").GetGuid()));

        var completeDetails = await GetDetailsAsync(client, completedCandidate.Id);
        var approved = await UpdateStatusAsync(
            client,
            completedCandidate.Id,
            ConsultationRequestStatus.Approved,
            null,
            completeDetails.GetProperty("rowVersion").GetString()!);
        var approvedBody = await approved.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var completed = await UpdateStatusAsync(
            client,
            completedCandidate.Id,
            ConsultationRequestStatus.Completed,
            null,
            approvedBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var completedBody = await completed.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.NotEqual(JsonValueKind.Null, completedBody.GetProperty("completedOnUtc").ValueKind);
        var completedTerminal = await UpdateStatusAsync(
            client,
            completedCandidate.Id,
            ConsultationRequestStatus.UnderReview,
            null,
            completedBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal((HttpStatusCode)422, completedTerminal.StatusCode);

        var clientApprovalDetails = await GetDetailsAsync(client, clientRequest.Id);
        var clientApproved = await UpdateStatusAsync(
            client,
            clientRequest.Id,
            ConsultationRequestStatus.Approved,
            null,
            clientApprovalDetails.GetProperty("rowVersion").GetString()!);
        Assert.Equal(HttpStatusCode.OK, clientApproved.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondLawyer.Token);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync(
                $"/api/v1/lawyer/consultation-requests/{firstGuest.Id}",
                TestContext.Current.CancellationToken)).StatusCode);
        var otherLawyerDetails = await GetDetailsAsync(client, otherLawyerRequest.Id);
        var otherApproved = await UpdateStatusAsync(
            client,
            otherLawyerRequest.Id,
            ConsultationRequestStatus.Approved,
            null,
            otherLawyerDetails.GetProperty("rowVersion").GetString()!);
        Assert.Equal(HttpStatusCode.OK, otherApproved.StatusCode);

        await using var notificationScope = factory.Services.CreateAsyncScope();
        var notificationContext = notificationScope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var firstTypes = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.AggregateId == firstGuest.Id)
            .Select(message => message.NotificationType)
            .ToListAsync(TestContext.Current.CancellationToken);
        var completedTypes = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.AggregateId == completedCandidate.Id)
            .Select(message => message.NotificationType)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, firstTypes.Count);
        Assert.Contains(EmailNotificationType.ConsultationUnderReview, firstTypes);
        Assert.Contains(EmailNotificationType.ConsultationRejected, firstTypes);
        Assert.DoesNotContain(EmailNotificationType.ConsultationApproved, firstTypes);
        Assert.Equal(4, completedTypes.Count);
        Assert.Contains(EmailNotificationType.ConsultationApproved, completedTypes);
        Assert.Contains(EmailNotificationType.ConsultationCompleted, completedTypes);
        var approvedWithMap = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.AggregateId == completedCandidate.Id &&
                message.NotificationType == EmailNotificationType.ConsultationApproved,
                TestContext.Current.CancellationToken);
        var approvedWithoutMap = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.AggregateId == otherLawyerRequest.Id &&
                message.NotificationType == EmailNotificationType.ConsultationApproved,
                TestContext.Current.CancellationToken);
        var createdConfirmation = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.AggregateId == completedCandidate.Id &&
                message.NotificationType == EmailNotificationType.ConsultationRequestCreatedConfirmation,
                TestContext.Current.CancellationToken);
        var clientApprovedWithMap = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.AggregateId == clientRequest.Id &&
                message.NotificationType == EmailNotificationType.ConsultationApproved,
                TestContext.Current.CancellationToken);
        Assert.Contains(
            "https://www.google.com/maps/search/?api=1&amp;query=30.044420,31.235712",
            approvedWithMap.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains(LawyerPublicPhoneNumber, approvedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("رقم هاتف المحامي:", approvedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Lawyer Phone Number:", approvedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("موقع مكتب المحامي", approvedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Lawyer Office Location", approvedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("01086111111", approvedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(LawyerPublicPhoneNumber, clientApprovedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("رقم هاتف المحامي:", clientApprovedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Lawyer Phone Number:", clientApprovedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("موقع مكتب المحامي", clientApprovedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Lawyer Office Location", clientApprovedWithMap.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(
            "https://www.google.com/maps/search/?api=1&amp;query=30.044420,31.235712",
            clientApprovedWithMap.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains(LawyerPublicPhoneNumber, approvedWithoutMap.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Office Location", approvedWithoutMap.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("google.com/maps", approvedWithoutMap.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(LawyerPublicPhoneNumber, createdConfirmation.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("رقم هاتف المحامي:", createdConfirmation.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Lawyer Phone Number:", createdConfirmation.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(
                "في حالة موافقة المحامي على طلب الاستشارة، سيتم إرسال موقع مكتب المحامي وفق القواعد الحالية للنظام.",
            createdConfirmation.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains(
                "If the lawyer approves your consultation request, the lawyer&#39;s office location will be sent according to the platform&#39;s current rules.",
            createdConfirmation.HtmlBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain("google.com/maps", createdConfirmation.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("30.044420", createdConfirmation.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("31.235712", createdConfirmation.HtmlBody, StringComparison.Ordinal);
    }

    private static async Task<(Guid ProfileId, Guid UserAccountId, string Token)> RegisterApproveAndLoginLawyerAsync(
        CustomWebApplicationFactory factory,
        HttpClient client,
        string userName,
        string phoneNumber,
        bool includeCoordinates)
    {
        const string password = "LawyerPassword1";
        client.DefaultRequestHeaders.Authorization = null;
        var register = await client.PostAsJsonAsync("/api/v1/auth/lawyers/register", new
        {
            fullName = $"{userName} Full Name",
            userName,
            email = $"{userName}@example.test",
            phoneNumber,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registered = await register.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var profileId = registered.GetProperty("lawyerProfileId").GetGuid();
        var userAccountId = registered.GetProperty("userAccountId").GetGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var profile = await context.LawyerProfiles
                .SingleAsync(item => item.Id == profileId, TestContext.Current.CancellationToken);
            var nowUtc = DateTime.UtcNow;
            profile.UpdateProfessionalProfile(profile.FullName, "Attorney", "Biography", 7, $"REG-{userName}");
            var area = EgyptLocationSeedCatalog.Areas[0];
            var city = EgyptLocationSeedCatalog.Cities.Single(item => item.Id == area.CityId);
            profile.UpsertPrimaryOffice(
                city.GovernorateId,
                city.Id,
                area.Id,
                "Complete address",
                LawyerPublicPhoneNumber,
                includeCoordinates ? 30.044420m : null,
                includeCoordinates ? 31.235712m : null);
            profile.ReplaceSpecializations([1]);
            profile.AddDocument("IdentityVerification", $"docs/{userName}-id.pdf", "id.pdf", "application/pdf", 100, nowUtc);
            profile.AddDocument("ProfessionalMembership", $"docs/{userName}-member.pdf", "member.pdf", "application/pdf", 100, nowUtc);
            profile.SubmitForApproval(userAccountId, true, true, nowUtc.AddMinutes(1));
            profile.Approve(Guid.Parse("11111111-1111-1111-1111-111111111111"), true, nowUtc.AddMinutes(2));
            context.LawyerOffices.AddRange(profile.Offices);
            context.LawyerSpecializations.AddRange(profile.Specializations);
            context.LawyerDocuments.AddRange(profile.Documents);
            context.LawyerApprovalStatusHistory.AddRange(profile.StatusHistory);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = userName,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (profileId, userAccountId, loginBody.GetProperty("accessToken").GetString()!);
    }

    private static async Task<(Guid Id, string ReferenceNumber)> CreateGuestRequestAsync(
        HttpClient client,
        Guid lawyerId,
        string fullName,
        string phoneNumber)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/public/consultation-requests", new
        {
            lawyerId,
            consultationType = "Onsite",
            fullName,
            phoneNumber,
            email = $"{fullName.Replace(" ", ".", StringComparison.Ordinal).ToLowerInvariant()}@example.test",
            description = $"Request from {fullName}"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("referenceNumber").GetString()!);
    }

    private static async Task<(Guid Id, string ReferenceNumber)> CreateClientRequestAsync(
        HttpClient client,
        Guid lawyerId)
    {
        const string password = "ClientPassword1";
        var register = await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName = "Request Client",
            userName = "request.client",
            email = "request.client@example.test",
            phoneNumber = "01086999999",
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = "request.client",
            password
        }, TestContext.Current.CancellationToken);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            loginBody.GetProperty("accessToken").GetString());
        var response = await client.PostAsJsonAsync("/api/v1/client/consultation-requests", new
        {
            lawyerId,
            consultationType = "Onsite",
            legalSpecializationId = 1,
            description = "Client consultation request"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("referenceNumber").GetString()!);
    }

    private static async Task<JsonElement> GetDetailsAsync(HttpClient client, Guid requestId)
    {
        var response = await client.GetAsync(
            $"/api/v1/lawyer/consultation-requests/{requestId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> UpdateStatusAsync(
        HttpClient client,
        Guid requestId,
        ConsultationRequestStatus status,
        string? reason,
        string rowVersion)
        => client.PutAsJsonAsync($"/api/v1/lawyer/consultation-requests/{requestId}/status", new
        {
            status,
            reason,
            rowVersion
        }, TestContext.Current.CancellationToken);

    private static async Task<string> ReadPrimaryCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("errors")[0].GetProperty("code").GetString()!;
    }
}
