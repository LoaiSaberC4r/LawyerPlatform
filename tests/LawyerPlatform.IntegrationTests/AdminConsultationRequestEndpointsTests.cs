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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using LawyerPlatform.Application.Notifications.Email;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Time;
using LawyerPlatform.Infrastructure.Email;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.IntegrationTests;

public sealed class AdminConsultationRequestEndpointsTests
{
    [Fact]
    public async Task SuperAdminCanFilterInspectAndTransitionAllRequestsWithConcurrencyAndTerminalRules()
    {
        await using var factory = new CustomWebApplicationFactory();
        await factory.SeedDatabaseAsync(TestContext.Current.CancellationToken);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        var lawyerId = await CreateEligibleLawyerAsync(factory);
        var first = await CreateGuestRequestAsync(client, lawyerId, "Admin Search Guest", "01088111111");
        var second = await CreateGuestRequestAsync(client, lawyerId, "Second Admin Guest", "01088222222");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/admin/consultation-requests", TestContext.Current.CancellationToken)).StatusCode);

        await RegisterAndLoginClientAsync(client);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/v1/admin/consultation-requests", TestContext.Current.CancellationToken)).StatusCode);

        var adminToken = await LoginAndChangeAdminPasswordAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var byReference = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/admin/consultation-requests?searchText={first.ReferenceNumber}&requesterType=Guest&status=New&lawyerId={lawyerId}",
            TestContext.Current.CancellationToken);
        Assert.Equal(1, byReference.GetProperty("totalItems").GetInt64());
        Assert.Equal(first.Id, byReference.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal("Onsite", byReference.GetProperty("items")[0].GetProperty("consultationType").GetString());
        Assert.Equal(JsonValueKind.Null, byReference.GetProperty("items")[0].GetProperty("consultationPrice").ValueKind);

        var byRequester = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/admin/consultation-requests?searchText=Admin%20Search%20Guest",
            TestContext.Current.CancellationToken);
        Assert.Equal(1, byRequester.GetProperty("totalItems").GetInt64());
        var byLawyer = await client.GetFromJsonAsync<JsonElement>(
            "/api/v1/admin/consultation-requests?searchText=Administrative%20Lawyer",
            TestContext.Current.CancellationToken);
        Assert.Equal(2, byLawyer.GetProperty("totalItems").GetInt64());

        var details = await GetDetailsAsync(client, first.Id);
        Assert.Equal("Guest", details.GetProperty("requesterType").GetString());
        Assert.Equal("Admin Search Guest", details.GetProperty("requesterFullName").GetString());
        Assert.Equal("01088111111", details.GetProperty("requesterPhoneNumber").GetString());
        Assert.Equal("Administrative Lawyer", details.GetProperty("lawyer").GetProperty("fullName").GetString());
        Assert.Equal("Admin request details", details.GetProperty("description").GetString());
        Assert.Equal("Onsite", details.GetProperty("consultationType").GetString());
        Assert.Equal(JsonValueKind.Null, details.GetProperty("consultationPrice").ValueKind);
        Assert.Equal(0, details.GetProperty("statusHistory").GetArrayLength());

        var originalRowVersion = details.GetProperty("rowVersion").GetString()!;
        var underReview = await UpdateStatusAsync(
            client, first.Id, ConsultationRequestStatus.UnderReview, null, originalRowVersion);
        Assert.Equal(HttpStatusCode.OK, underReview.StatusCode);
        var underReviewBody = await underReview.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        var stale = await UpdateStatusAsync(
            client, first.Id, ConsultationRequestStatus.Approved, null, originalRowVersion);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ConsultationRequest.ConcurrencyConflict", await ReadCodeAsync(stale));

        var invalid = await UpdateStatusAsync(
            client,
            first.Id,
            ConsultationRequestStatus.Completed,
            null,
            underReviewBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal((HttpStatusCode)422, invalid.StatusCode);
        Assert.Equal("ConsultationRequest.InvalidStatusTransition", await ReadCodeAsync(invalid));

        var missingReason = await UpdateStatusAsync(
            client,
            first.Id,
            ConsultationRequestStatus.Rejected,
            "",
            underReviewBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal((HttpStatusCode)422, missingReason.StatusCode);

        var rejected = await UpdateStatusAsync(
            client,
            first.Id,
            ConsultationRequestStatus.Rejected,
            "  Administrative rejection  ",
            underReviewBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var terminal = await UpdateStatusAsync(
            client,
            first.Id,
            ConsultationRequestStatus.Approved,
            null,
            rejectedBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal((HttpStatusCode)422, terminal.StatusCode);

        var rejectedDetails = await GetDetailsAsync(client, first.Id);
        Assert.Equal("Administrative rejection", rejectedDetails.GetProperty("rejectionReason").GetString());
        Assert.Equal(2, rejectedDetails.GetProperty("statusHistory").GetArrayLength());
        Assert.All(
            rejectedDetails.GetProperty("statusHistory").EnumerateArray(),
            history => Assert.Equal(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                history.GetProperty("changedByUserId").GetGuid()));

        var secondDetails = await GetDetailsAsync(client, second.Id);
        var approved = await UpdateStatusAsync(
            client,
            second.Id,
            ConsultationRequestStatus.Approved,
            null,
            secondDetails.GetProperty("rowVersion").GetString()!);
        var approvedBody = await approved.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var completed = await UpdateStatusAsync(
            client,
            second.Id,
            ConsultationRequestStatus.Completed,
            null,
            approvedBody.GetProperty("rowVersion").GetString()!);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);

        await using var notificationScope = factory.Services.CreateAsyncScope();
        var notificationContext = notificationScope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var firstTypes = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.AggregateId == first.Id)
            .Select(message => message.NotificationType)
            .ToListAsync(TestContext.Current.CancellationToken);
        var secondTypes = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.AggregateId == second.Id)
            .Select(message => message.NotificationType)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, firstTypes.Count);
        Assert.Contains(EmailNotificationType.ConsultationUnderReview, firstTypes);
        Assert.Contains(EmailNotificationType.ConsultationRejected, firstTypes);
        Assert.DoesNotContain(EmailNotificationType.ConsultationApproved, firstTypes);
        Assert.Equal(4, secondTypes.Count);
        Assert.Contains(EmailNotificationType.ConsultationApproved, secondTypes);
        Assert.Contains(EmailNotificationType.ConsultationCompleted, secondTypes);
        var adminApprovedMessage = await notificationContext.EmailOutboxMessages
            .AsNoTracking()
            .SingleAsync(message =>
                message.AggregateId == second.Id &&
                message.NotificationType == EmailNotificationType.ConsultationApproved,
                TestContext.Current.CancellationToken);
        Assert.Contains(
            "https://www.google.com/maps/search/?api=1&amp;query=30.044420,31.235712",
            adminApprovedMessage.HtmlBody,
            StringComparison.Ordinal);
        var footerImageUrl = notificationScope.ServiceProvider
            .GetRequiredService<IOptions<EmailBrandingOptions>>()
            .Value
            .FooterImageUrl;
        Assert.Contains(footerImageUrl, adminApprovedMessage.HtmlBody, StringComparison.Ordinal);
        Assert.Equal(
            1,
            adminApprovedMessage.HtmlBody.Split(
                footerImageUrl,
                StringSplitOptions.None).Length - 1);
        Assert.True(
            adminApprovedMessage.HtmlBody.IndexOf("lang=\"en\"", StringComparison.Ordinal) <
            adminApprovedMessage.HtmlBody.IndexOf(footerImageUrl, StringComparison.Ordinal));

        var processor = new EmailOutboxProcessor(
            notificationContext,
            new FailingEmailSender(),
            notificationScope.ServiceProvider.GetRequiredService<IDateTimeProvider>(),
            notificationScope.ServiceProvider.GetRequiredService<IOptions<EmailOutboxOptions>>(),
            NullLogger<EmailOutboxProcessor>.Instance);
        Assert.True(await processor.ProcessBatchAsync(TestContext.Current.CancellationToken) > 0);
        notificationContext.ChangeTracker.Clear();
        Assert.Equal(
            ConsultationRequestStatus.Rejected,
            await notificationContext.ConsultationRequests
                .Where(request => request.Id == first.Id)
                .Select(request => request.Status)
                .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            ConsultationRequestStatus.Completed,
            await notificationContext.ConsultationRequests
                .Where(request => request.Id == second.Id)
                .Select(request => request.Status)
                .SingleAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<JsonElement> GetDetailsAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync(
            $"/api/v1/admin/consultation-requests/{id}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> UpdateStatusAsync(
        HttpClient client,
        Guid id,
        ConsultationRequestStatus status,
        string? reason,
        string rowVersion)
        => client.PutAsJsonAsync(
            $"/api/v1/admin/consultation-requests/{id}/status",
            new { status, reason, rowVersion },
            TestContext.Current.CancellationToken);

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
            legalSpecializationId = 1,
            fullName,
            phoneNumber,
            email = "admin.requester@example.test",
            description = "Admin request details"
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return (body.GetProperty("id").GetGuid(), body.GetProperty("referenceNumber").GetString()!);
    }

    private static async Task RegisterAndLoginClientAsync(HttpClient client)
    {
        const string password = "ClientPassword1";
        var register = await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName = "Unauthorized Client",
            userName = "admin.denied.client",
            email = "admin.denied.client@example.test",
            phoneNumber = "01088333333",
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var token = await LoginAsync(client, "admin.denied.client", password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<string> LoginAsync(HttpClient client, string userName, string password)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = userName,
            password
        }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("accessToken").GetString()!;
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
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("accessToken").GetString()!;
    }

    private static async Task<Guid> CreateEligibleLawyerAsync(CustomWebApplicationFactory factory)
    {
        var nowUtc = DateTime.UtcNow;
        var account = UserAccount.CreateLawyer(
            "administrative.lawyer", "ADMINISTRATIVE.LAWYER",
            "administrative.lawyer@example.test", "ADMINISTRATIVE.LAWYER@EXAMPLE.TEST",
            "01088000000", "hash", nowUtc).Value;
        var profile = LawyerProfile.Create(account, "Administrative Lawyer").Value;
        profile.UpdateProfessionalProfile("Administrative Lawyer", "Attorney", "Biography", 9, "REG-ADMIN");
        var area = EgyptLocationSeedCatalog.Areas[0];
        var city = EgyptLocationSeedCatalog.Cities.Single(item => item.Id == area.CityId);
        profile.UpsertPrimaryOffice(
            city.GovernorateId,
            city.Id,
            area.Id,
            "Complete address",
            null,
            30.044420m,
            31.235712m);
        profile.ReplaceSpecializations([1]);
        profile.AddDocument("IdentityVerification", "docs/admin-id.pdf", "id.pdf", "application/pdf", 100, nowUtc);
        profile.AddDocument("ProfessionalMembership", "docs/admin-member.pdf", "member.pdf", "application/pdf", 100, nowUtc);
        profile.SubmitForApproval(account.Id, true, true, nowUtc.AddMinutes(1));
        profile.Approve(Guid.Parse("11111111-1111-1111-1111-111111111111"), true, nowUtc.AddMinutes(2));
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        context.LawyerProfiles.Add(profile);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return profile.Id;
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    private sealed class FailingEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
            => throw new IOException("simulated SMTP provider failure");
    }
}
