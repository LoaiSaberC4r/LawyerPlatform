using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LawyerPlatform.Application.Notifications.Email;

namespace LawyerPlatform.IntegrationTests;

public sealed class ClientProfileAndAdminClientManagementEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task ClientProfileAndAdminLifecycle_EnforceIdentityConcurrencyTransitionsAndLoginStatus()
    {
        using var client = PhaseOneTestHelpers.CreateClient(factory);
        var cancellationToken = TestContext.Current.CancellationToken;
        await factory.SeedDatabaseAsync(cancellationToken);

        var first = await PhaseOneTestHelpers.RegisterClientAsync(
            client,
            "phase1.client.one",
            "phase1.client.one@example.test",
            "01100000001",
            "Client One",
            cancellationToken);
        var second = await PhaseOneTestHelpers.RegisterClientAsync(
            client,
            "phase1.client.two",
            "phase1.client.two@example.test",
            "01100000002",
            "Client Two",
            cancellationToken);
        var firstToken = await PhaseOneTestHelpers.LoginAndReadTokenAsync(
            client,
            "phase1.client.one",
            PhaseOneTestHelpers.ClientPassword,
            cancellationToken);
        var secondToken = await PhaseOneTestHelpers.LoginAndReadTokenAsync(
            client,
            "phase1.client.two",
            PhaseOneTestHelpers.ClientPassword,
            cancellationToken);

        PhaseOneTestHelpers.UseToken(client, firstToken);
        var ownProfile = await PhaseOneTestHelpers.GetJsonAsync(client, "/api/v1/client/profile", cancellationToken);
        Assert.Equal(first.ClientProfileId, ownProfile.GetProperty("id").GetGuid());
        Assert.Equal(first.UserAccountId, ownProfile.GetProperty("account").GetProperty("id").GetGuid());
        Assert.False(ownProfile.GetProperty("account").TryGetProperty("passwordHash", out _));
        var originalProfileRowVersion = ownProfile.GetProperty("rowVersion").GetString()!;

        var invalidName = await client.PutAsJsonAsync("/api/v1/client/profile", new
        {
            fullName = "   ",
            rowVersion = originalProfileRowVersion
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidName.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeEndsWithAsync(
            invalidName,
            "Account.FullNameRequired",
            cancellationToken);

        var invalidProfileRowVersion = await client.PutAsJsonAsync("/api/v1/client/profile", new
        {
            fullName = "Client One Updated",
            rowVersion = "not-base64"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidProfileRowVersion.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeEndsWithAsync(
            invalidProfileRowVersion,
            "Client.InvalidRowVersion",
            cancellationToken);

        var update = await client.PutAsJsonAsync("/api/v1/client/profile", new
        {
            fullName = "  Client One Updated  ",
            rowVersion = originalProfileRowVersion
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("Client One Updated", updated.GetProperty("fullName").GetString());
        var updatedProfileRowVersion = updated.GetProperty("rowVersion").GetString()!;
        Assert.NotEqual(originalProfileRowVersion, updatedProfileRowVersion);

        var staleProfileUpdate = await client.PutAsJsonAsync("/api/v1/client/profile", new
        {
            fullName = "Stale Client Name",
            rowVersion = originalProfileRowVersion
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleProfileUpdate.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeAsync(
            staleProfileUpdate,
            "Client.ConcurrencyConflict",
            cancellationToken);

        PhaseOneTestHelpers.UseToken(client, secondToken);
        var secondProfile = await PhaseOneTestHelpers.GetJsonAsync(client, "/api/v1/client/profile", cancellationToken);
        Assert.Equal(second.ClientProfileId, secondProfile.GetProperty("id").GetGuid());
        Assert.Equal("Client Two", secondProfile.GetProperty("fullName").GetString());

        var adminToken = await PhaseOneTestHelpers.LoginAndChangeAdminPasswordAsync(client, cancellationToken);
        PhaseOneTestHelpers.UseToken(client, adminToken);
        var firstPage = await PhaseOneTestHelpers.GetJsonAsync(
            client,
            "/api/v1/admin/clients?pageNumber=1&pageSize=1",
            cancellationToken);
        Assert.Equal(1, firstPage.GetProperty("pageNumber").GetInt32());
        Assert.Equal(1, firstPage.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, firstPage.GetProperty("totalItems").GetInt64());
        Assert.Single(firstPage.GetProperty("items").EnumerateArray());
        Assert.True(firstPage.GetProperty("hasNextPage").GetBoolean());
        var repeatedFirstPage = await PhaseOneTestHelpers.GetJsonAsync(
            client,
            "/api/v1/admin/clients?pageNumber=1&pageSize=1",
            cancellationToken);
        Assert.Equal(
            firstPage.GetProperty("items")[0].GetProperty("id").GetGuid(),
            repeatedFirstPage.GetProperty("items")[0].GetProperty("id").GetGuid());

        var details = await PhaseOneTestHelpers.GetJsonAsync(
            client,
            $"/api/v1/admin/clients/{first.ClientProfileId}",
            cancellationToken);
        Assert.Equal(first.ClientProfileId, details.GetProperty("id").GetGuid());
        Assert.Equal("Client One Updated", details.GetProperty("fullName").GetString());
        Assert.Equal(updatedProfileRowVersion, details.GetProperty("profileRowVersion").GetString());
        var accountRowVersion = details.GetProperty("account").GetProperty("rowVersion").GetString()!;

        var unknown = await client.GetAsync($"/api/v1/admin/clients/{Guid.NewGuid()}", cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeAsync(unknown, "Client.NotFound", cancellationToken);

        var invalidAccountRowVersion = await client.PostAsJsonAsync(
            $"/api/v1/admin/clients/{first.ClientProfileId}/suspend",
            new { rowVersion = "not-base64" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidAccountRowVersion.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeEndsWithAsync(
            invalidAccountRowVersion,
            "Account.InvalidRowVersion",
            cancellationToken);

        var suspend = await client.PostAsJsonAsync(
            $"/api/v1/admin/clients/{first.ClientProfileId}/suspend",
            new { rowVersion = accountRowVersion },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);
        var suspended = await suspend.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("Suspended", suspended.GetProperty("accountStatus").GetString());
        var suspendedRowVersion = suspended.GetProperty("rowVersion").GetString()!;
        Assert.NotEqual(accountRowVersion, suspendedRowVersion);

        client.DefaultRequestHeaders.Authorization = null;
        var suspendedLogin = await PhaseOneTestHelpers.LoginAsync(
            client,
            "phase1.client.one",
            PhaseOneTestHelpers.ClientPassword,
            cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, suspendedLogin.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeAsync(suspendedLogin, "Account.Suspended", cancellationToken);

        PhaseOneTestHelpers.UseToken(client, adminToken);
        var repeatedSuspend = await client.PostAsJsonAsync(
            $"/api/v1/admin/clients/{first.ClientProfileId}/suspend",
            new { rowVersion = suspendedRowVersion },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repeatedSuspend.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeAsync(
            repeatedSuspend,
            "Account.InvalidStatusTransition",
            cancellationToken);

        var staleReactivate = await client.PostAsJsonAsync(
            $"/api/v1/admin/clients/{first.ClientProfileId}/reactivate",
            new { rowVersion = accountRowVersion },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, staleReactivate.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeAsync(
            staleReactivate,
            "Account.ConcurrencyConflict",
            cancellationToken);

        var reactivate = await client.PostAsJsonAsync(
            $"/api/v1/admin/clients/{first.ClientProfileId}/reactivate",
            new { rowVersion = suspendedRowVersion },
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        var reactivated = await reactivate.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("Active", reactivated.GetProperty("accountStatus").GetString());
        var activeRowVersion = reactivated.GetProperty("rowVersion").GetString()!;
        Assert.NotEqual(suspendedRowVersion, activeRowVersion);

        var repeatedReactivate = await client.PostAsJsonAsync(
            $"/api/v1/admin/clients/{first.ClientProfileId}/reactivate",
            new { rowVersion = activeRowVersion },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, repeatedReactivate.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeAsync(
            repeatedReactivate,
            "Account.InvalidStatusTransition",
            cancellationToken);

        client.DefaultRequestHeaders.Authorization = null;
        var activeLogin = await PhaseOneTestHelpers.LoginAsync(
            client,
            "phase1.client.one",
            PhaseOneTestHelpers.ClientPassword,
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, activeLogin.StatusCode);

        await using (var notificationScope = factory.Services.CreateAsyncScope())
        {
            var notificationContext = notificationScope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var notifications = await notificationContext.EmailOutboxMessages
                .AsNoTracking()
                .Where(message => message.AggregateId == first.ClientProfileId)
                .ToListAsync(cancellationToken);
            Assert.Equal(2, notifications.Count);
            Assert.Contains(notifications, message =>
                message.NotificationType == EmailNotificationType.ClientSuspended &&
                message.RecipientEmail == "phase1.client.one@example.test");
            Assert.Contains(notifications, message =>
                message.NotificationType == EmailNotificationType.ClientReactivated &&
                message.RecipientEmail == "phase1.client.one@example.test");
            Assert.All(notifications, message =>
                Assert.DoesNotContain("password", message.HtmlBody, StringComparison.OrdinalIgnoreCase));
        }

        string inactiveRowVersion;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var account = await dbContext.UserAccounts.SingleAsync(
                item => item.Id == first.UserAccountId,
                cancellationToken);
            Assert.True(account.Deactivate(DateTime.UtcNow).IsSuccess);
            await dbContext.SaveChangesAsync(cancellationToken);
            inactiveRowVersion = Convert.ToBase64String(account.RowVersion);
        }

        PhaseOneTestHelpers.UseToken(client, adminToken);
        var reactivateInactive = await client.PostAsJsonAsync(
            $"/api/v1/admin/clients/{first.ClientProfileId}/reactivate",
            new { rowVersion = inactiveRowVersion },
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reactivateInactive.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeAsync(
            reactivateInactive,
            "Account.InvalidStatusTransition",
            cancellationToken);

        PhaseOneTestHelpers.UseToken(client, secondToken);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var profile = await dbContext.ClientProfiles.SingleAsync(
                item => item.Id == second.ClientProfileId,
                cancellationToken);
            profile.IsDeleted = true;
            profile.DeletedOnUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/client/profile", cancellationToken)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/client/dashboard", cancellationToken)).StatusCode);
        var missingUpdate = await client.PutAsJsonAsync("/api/v1/client/profile", new
        {
            fullName = "Cannot Update",
            rowVersion = secondProfile.GetProperty("rowVersion").GetString()
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingUpdate.StatusCode);
        await PhaseOneTestHelpers.AssertProblemCodeAsync(missingUpdate, "Client.NotFound", cancellationToken);
    }
}

public sealed class DashboardEndpointsTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Dashboards_CountOnlyTheApprovedScopesAndExcludeSoftDeletedProfiles()
    {
        using var client = PhaseOneTestHelpers.CreateClient(factory);
        var cancellationToken = TestContext.Current.CancellationToken;
        await factory.SeedDatabaseAsync(cancellationToken);

        var currentClient = await PhaseOneTestHelpers.RegisterClientAsync(
            client, "dashboard.client.a", "dashboard.client.a@example.test", "01200000001", "Dashboard Client A", cancellationToken);
        var otherClient = await PhaseOneTestHelpers.RegisterClientAsync(
            client, "dashboard.client.b", "dashboard.client.b@example.test", "01200000002", "Dashboard Client B", cancellationToken);
        var lawyerA = await PhaseOneTestHelpers.RegisterLawyerAsync(
            client, "dashboard.lawyer.a", "dashboard.lawyer.a@example.test", "01200000003", "Dashboard Lawyer A", cancellationToken);
        var lawyerB = await PhaseOneTestHelpers.RegisterLawyerAsync(
            client, "dashboard.lawyer.b", "dashboard.lawyer.b@example.test", "01200000004", "Dashboard Lawyer B", cancellationToken);

        await SeedDashboardDataAsync(
            currentClient.UserAccountId,
            currentClient.ClientProfileId,
            otherClient.ClientProfileId,
            lawyerA.LawyerProfileId,
            lawyerB.LawyerProfileId,
            cancellationToken);

        var clientToken = await PhaseOneTestHelpers.LoginAndReadTokenAsync(
            client, "dashboard.client.a", PhaseOneTestHelpers.ClientPassword, cancellationToken);
        PhaseOneTestHelpers.UseToken(client, clientToken);
        var clientDashboard = await PhaseOneTestHelpers.GetJsonAsync(
            client, "/api/v1/client/dashboard", cancellationToken);
        AssertRequestDashboard(clientDashboard, total: 6, newCount: 2, underReview: 1, approved: 1, rejected: 1, completed: 1);

        var lawyerToken = await PhaseOneTestHelpers.LoginAndReadTokenAsync(
            client, "dashboard.lawyer.a", PhaseOneTestHelpers.LawyerPassword, cancellationToken);
        PhaseOneTestHelpers.UseToken(client, lawyerToken);
        var lawyerDashboard = await PhaseOneTestHelpers.GetJsonAsync(
            client, "/api/v1/lawyer/dashboard", cancellationToken);
        AssertRequestDashboard(lawyerDashboard, total: 6, newCount: 2, underReview: 1, approved: 1, rejected: 1, completed: 1);

        var adminToken = await PhaseOneTestHelpers.LoginAndChangeAdminPasswordAsync(client, cancellationToken);
        PhaseOneTestHelpers.UseToken(client, adminToken);
        var adminDashboard = await PhaseOneTestHelpers.GetJsonAsync(
            client, "/api/v1/admin/dashboard", cancellationToken);
        var lawyers = adminDashboard.GetProperty("lawyers");
        Assert.Equal(8, lawyers.GetProperty("total").GetInt64());
        Assert.Equal(1, lawyers.GetProperty("pendingApproval").GetInt64());
        Assert.Equal(1, lawyers.GetProperty("approved").GetInt64());
        Assert.Equal(1, lawyers.GetProperty("suspended").GetInt64());
        Assert.Equal(2, adminDashboard.GetProperty("clients").GetProperty("total").GetInt64());
        var consultations = adminDashboard.GetProperty("consultationRequests");
        Assert.Equal(8, consultations.GetProperty("total").GetInt64());
        var byStatus = consultations.GetProperty("byStatus");
        Assert.Equal(3, byStatus.GetProperty("new").GetInt64());
        Assert.Equal(1, byStatus.GetProperty("underReview").GetInt64());
        Assert.Equal(1, byStatus.GetProperty("approved").GetInt64());
        Assert.Equal(1, byStatus.GetProperty("rejected").GetInt64());
        Assert.Equal(2, byStatus.GetProperty("completed").GetInt64());
    }

    private async Task SeedDashboardDataAsync(
        Guid actorId,
        Guid clientAId,
        Guid clientBId,
        Guid lawyerAId,
        Guid lawyerBId,
        CancellationToken cancellationToken)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        var nowUtc = DateTime.UtcNow;

        var representativeLawyers = new List<(UserAccount Account, LawyerProfile Profile)>();
        for (var index = 0; index < 6; index++)
        {
            var account = UserAccount.CreateLawyer(
                $"dashboard.metric.{index}",
                $"DASHBOARD.METRIC.{index}",
                $"dashboard.metric.{index}@example.test",
                $"DASHBOARD.METRIC.{index}@EXAMPLE.TEST",
                $"0130000000{index}",
                "integration-test-hash",
                nowUtc).Value;
            var profile = LawyerProfile.Create(account, $"Metric Lawyer {index}").Value;
            representativeLawyers.Add((account, profile));
        }

        Assert.True(representativeLawyers[1].Profile.SubmitForApproval(actorId, true, true, nowUtc).IsSuccess);
        Assert.True(representativeLawyers[2].Profile.SubmitForApproval(actorId, true, true, nowUtc).IsSuccess);
        Assert.True(representativeLawyers[2].Profile.RequestChanges(actorId, "Complete the profile", nowUtc).IsSuccess);
        Assert.True(representativeLawyers[3].Profile.SubmitForApproval(actorId, true, true, nowUtc).IsSuccess);
        Assert.True(representativeLawyers[3].Profile.Approve(actorId, true, nowUtc).IsSuccess);
        Assert.True(representativeLawyers[4].Profile.SubmitForApproval(actorId, true, true, nowUtc).IsSuccess);
        Assert.True(representativeLawyers[4].Profile.Reject(actorId, "Application rejected", nowUtc).IsSuccess);
        Assert.True(representativeLawyers[5].Profile.SubmitForApproval(actorId, true, true, nowUtc).IsSuccess);
        Assert.True(representativeLawyers[5].Profile.Approve(actorId, true, nowUtc).IsSuccess);
        Assert.True(representativeLawyers[5].Profile.Suspend(actorId, "Temporarily suspended", nowUtc).IsSuccess);

        var deletedLawyerAccount = UserAccount.CreateLawyer(
            "dashboard.deleted.lawyer",
            "DASHBOARD.DELETED.LAWYER",
            "dashboard.deleted.lawyer@example.test",
            "DASHBOARD.DELETED.LAWYER@EXAMPLE.TEST",
            "01300000009",
            "integration-test-hash",
            nowUtc).Value;
        var deletedLawyer = LawyerProfile.Create(deletedLawyerAccount, "Deleted Lawyer").Value;
        deletedLawyer.IsDeleted = true;
        deletedLawyer.DeletedOnUtc = nowUtc;

        var deletedClientAccount = UserAccount.CreateClient(
            "dashboard.deleted.client",
            "DASHBOARD.DELETED.CLIENT",
            "dashboard.deleted.client@example.test",
            "DASHBOARD.DELETED.CLIENT@EXAMPLE.TEST",
            "01300000010",
            "integration-test-hash",
            nowUtc).Value;
        var deletedClient = ClientProfile.Create(deletedClientAccount, "Deleted Client").Value;
        deletedClient.IsDeleted = true;
        deletedClient.DeletedOnUtc = nowUtc;

        foreach (var pair in representativeLawyers)
        {
            dbContext.AddRange(pair.Account, pair.Profile);
        }

        dbContext.AddRange(deletedLawyerAccount, deletedLawyer, deletedClientAccount, deletedClient);

        var requests = new[]
        {
            CreateRequest("DASH-001", clientAId, lawyerAId, ConsultationRequestStatus.New, actorId, nowUtc),
            CreateRequest("DASH-002", clientAId, lawyerAId, ConsultationRequestStatus.UnderReview, actorId, nowUtc),
            CreateRequest("DASH-003", clientAId, lawyerAId, ConsultationRequestStatus.Approved, actorId, nowUtc),
            CreateRequest("DASH-004", clientAId, lawyerAId, ConsultationRequestStatus.Rejected, actorId, nowUtc),
            CreateRequest("DASH-005", clientAId, lawyerAId, ConsultationRequestStatus.Completed, actorId, nowUtc),
            CreateRequest("DASH-006", clientAId, lawyerBId, ConsultationRequestStatus.New, actorId, nowUtc),
            CreateRequest("DASH-007", clientBId, lawyerAId, ConsultationRequestStatus.New, actorId, nowUtc),
            CreateRequest("DASH-008", clientBId, lawyerBId, ConsultationRequestStatus.Completed, actorId, nowUtc)
        };
        dbContext.ConsultationRequests.AddRange(requests);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ConsultationRequest CreateRequest(
        string referenceNumber,
        Guid clientProfileId,
        Guid lawyerProfileId,
        ConsultationRequestStatus status,
        Guid actorId,
        DateTime nowUtc)
    {
        var request = ConsultationRequest.CreateForClient(
            referenceNumber,
            clientProfileId,
            lawyerProfileId,
            null,
            "Dashboard test request",
            null,
            nowUtc).Value;
        switch (status)
        {
            case ConsultationRequestStatus.UnderReview:
                Assert.True(request.ChangeStatus(status, actorId, null, nowUtc).IsSuccess);
                break;
            case ConsultationRequestStatus.Approved:
                Assert.True(request.ChangeStatus(status, actorId, null, nowUtc).IsSuccess);
                break;
            case ConsultationRequestStatus.Rejected:
                Assert.True(request.ChangeStatus(status, actorId, "Rejected for test", nowUtc).IsSuccess);
                break;
            case ConsultationRequestStatus.Completed:
                Assert.True(request.ChangeStatus(ConsultationRequestStatus.Approved, actorId, null, nowUtc).IsSuccess);
                Assert.True(request.ChangeStatus(ConsultationRequestStatus.Completed, actorId, null, nowUtc).IsSuccess);
                break;
        }

        return request;
    }

    private static void AssertRequestDashboard(
        JsonElement dashboard,
        long total,
        long newCount,
        long underReview,
        long approved,
        long rejected,
        long completed)
    {
        Assert.Equal(total, dashboard.GetProperty("totalRequests").GetInt64());
        Assert.Equal(newCount, dashboard.GetProperty("newRequests").GetInt64());
        Assert.Equal(underReview, dashboard.GetProperty("underReviewRequests").GetInt64());
        Assert.Equal(approved, dashboard.GetProperty("approvedRequests").GetInt64());
        Assert.Equal(rejected, dashboard.GetProperty("rejectedRequests").GetInt64());
        Assert.Equal(completed, dashboard.GetProperty("completedRequests").GetInt64());
    }
}

public sealed class PhaseOneAuthorizationSwaggerAndCatalogRemovalTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task SwaggerContainsNineRoutesCatalogIsGoneAndAuthorizationMatrixIsEnforced()
    {
        using var client = PhaseOneTestHelpers.CreateClient(factory);
        var cancellationToken = TestContext.Current.CancellationToken;
        await factory.SeedDatabaseAsync(cancellationToken);

        var swagger = await PhaseOneTestHelpers.GetJsonAsync(client, "/swagger/v1/swagger.json", cancellationToken);
        var paths = swagger.GetProperty("paths");
        string[] expectedPaths =
        [
            "/api/v1/client/profile",
            "/api/v1/client/dashboard",
            "/api/v1/lawyer/dashboard",
            "/api/v1/admin/clients",
            "/api/v1/admin/clients/{clientId}",
            "/api/v1/admin/clients/{clientId}/suspend",
            "/api/v1/admin/clients/{clientId}/reactivate",
            "/api/v1/admin/dashboard"
        ];
        foreach (var path in expectedPaths)
        {
            Assert.True(paths.TryGetProperty(path, out _), $"Swagger path '{path}' is missing.");
        }

        Assert.True(paths.GetProperty("/api/v1/client/profile").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/client/profile").TryGetProperty("put", out _));
        var expectedOperations = new Dictionary<string, string>
        {
            ["/api/v1/client/profile"] = "get,put",
            ["/api/v1/client/dashboard"] = "get",
            ["/api/v1/lawyer/dashboard"] = "get",
            ["/api/v1/admin/clients"] = "get",
            ["/api/v1/admin/clients/{clientId}"] = "get",
            ["/api/v1/admin/clients/{clientId}/suspend"] = "post",
            ["/api/v1/admin/clients/{clientId}/reactivate"] = "post",
            ["/api/v1/admin/dashboard"] = "get"
        };
        foreach (var (path, methods) in expectedOperations)
        {
            foreach (var method in methods.Split(','))
            {
                var operation = paths.GetProperty(path).GetProperty(method);
                Assert.NotEmpty(operation.GetProperty("security").EnumerateArray());
                var responses = operation.GetProperty("responses");
                Assert.True(responses.GetProperty("200").GetProperty("content").TryGetProperty("application/json", out _));
                Assert.True(responses.GetProperty("422").GetProperty("content").TryGetProperty("application/problem+json", out _));
            }
        }

        var clientIdParameter = paths
            .GetProperty("/api/v1/admin/clients/{clientId}")
            .GetProperty("get")
            .GetProperty("parameters")
            .EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "clientId");
        Assert.Contains("ClientProfile.Id", clientIdParameter.GetProperty("description").GetString());
        Assert.DoesNotContain(
            paths.EnumerateObject(),
            path => path.Name.Contains("catalog-items", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/catalog-items", cancellationToken)).StatusCode);

        var anonymousRequests = new HttpRequestMessage[]
        {
            new(HttpMethod.Get, "/api/v1/client/profile"),
            new(HttpMethod.Put, "/api/v1/client/profile") { Content = JsonContent.Create(new { fullName = "Name", rowVersion = "AQ==" }) },
            new(HttpMethod.Get, "/api/v1/client/dashboard"),
            new(HttpMethod.Get, "/api/v1/lawyer/dashboard"),
            new(HttpMethod.Get, "/api/v1/admin/clients"),
            new(HttpMethod.Get, $"/api/v1/admin/clients/{Guid.NewGuid()}"),
            new(HttpMethod.Post, $"/api/v1/admin/clients/{Guid.NewGuid()}/suspend") { Content = JsonContent.Create(new { rowVersion = "AQ==" }) },
            new(HttpMethod.Post, $"/api/v1/admin/clients/{Guid.NewGuid()}/reactivate") { Content = JsonContent.Create(new { rowVersion = "AQ==" }) },
            new(HttpMethod.Get, "/api/v1/admin/dashboard")
        };
        foreach (var request in anonymousRequests)
        {
            using (request)
            using (var response = await client.SendAsync(request, cancellationToken))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }

        var adminToken = await PhaseOneTestHelpers.LoginAndChangeAdminPasswordAsync(client, cancellationToken);
        PhaseOneTestHelpers.UseToken(client, adminToken);
        var emptyClients = await PhaseOneTestHelpers.GetJsonAsync(client, "/api/v1/admin/clients", cancellationToken);
        Assert.Equal(0, emptyClients.GetProperty("totalItems").GetInt64());
        Assert.Empty(emptyClients.GetProperty("items").EnumerateArray());

        var registeredClient = await PhaseOneTestHelpers.RegisterClientAsync(
            client, "authorization.client", "authorization.client@example.test", "01400000001", "Authorization Client", cancellationToken);
        var registeredLawyer = await PhaseOneTestHelpers.RegisterLawyerAsync(
            client, "authorization.lawyer", "authorization.lawyer@example.test", "01400000002", "Authorization Lawyer", cancellationToken);
        var clientToken = await PhaseOneTestHelpers.LoginAndReadTokenAsync(
            client, "authorization.client", PhaseOneTestHelpers.ClientPassword, cancellationToken);
        var lawyerToken = await PhaseOneTestHelpers.LoginAndReadTokenAsync(
            client, "authorization.lawyer", PhaseOneTestHelpers.LawyerPassword, cancellationToken);

        PhaseOneTestHelpers.UseToken(client, clientToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/client/profile", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/client/dashboard", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/lawyer/dashboard", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/dashboard", cancellationToken)).StatusCode);

        PhaseOneTestHelpers.UseToken(client, lawyerToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/lawyer/dashboard", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/client/profile", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/clients", cancellationToken)).StatusCode);

        await using (var missingLawyerScope = factory.Services.CreateAsyncScope())
        {
            var missingLawyerDbContext = missingLawyerScope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            var profile = await missingLawyerDbContext.LawyerProfiles.SingleAsync(
                item => item.Id == registeredLawyer.LawyerProfileId,
                cancellationToken);
            profile.IsDeleted = true;
            profile.DeletedOnUtc = DateTime.UtcNow;
            await missingLawyerDbContext.SaveChangesAsync(cancellationToken);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/lawyer/dashboard", cancellationToken)).StatusCode);

        PhaseOneTestHelpers.UseToken(client, adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/dashboard", cancellationToken)).StatusCode);
        var list = await PhaseOneTestHelpers.GetJsonAsync(client, "/api/v1/admin/clients", cancellationToken);
        Assert.Equal(1, list.GetProperty("totalItems").GetInt64());
        Assert.Equal(registeredClient.ClientProfileId, list.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/client/profile", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/lawyer/dashboard", cancellationToken)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
        Assert.DoesNotContain(
            dbContext.Model.GetEntityTypes(),
            entity => entity.ClrType.Name == "CatalogItem");
        Assert.NotEqual(Guid.Empty, registeredLawyer.LawyerProfileId);
    }
}

internal static class PhaseOneTestHelpers
{
    public const string ClientPassword = "ClientPassword1";
    public const string LawyerPassword = "LawyerPassword1";

    public static HttpClient CreateClient(CustomWebApplicationFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    public static async Task<RegisteredClient> RegisterClientAsync(
        HttpClient client,
        string userName,
        string email,
        string phoneNumber,
        string fullName,
        CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/clients/register", new
        {
            fullName,
            userName,
            email,
            phoneNumber,
            password = ClientPassword
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return new RegisteredClient(
            body.GetProperty("userAccountId").GetGuid(),
            body.GetProperty("clientProfileId").GetGuid());
    }

    public static async Task<RegisteredLawyer> RegisterLawyerAsync(
        HttpClient client,
        string userName,
        string email,
        string phoneNumber,
        string fullName,
        CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/v1/auth/lawyers/register", new
        {
            fullName,
            userName,
            email,
            phoneNumber,
            password = LawyerPassword
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return new RegisteredLawyer(
            body.GetProperty("userAccountId").GetGuid(),
            body.GetProperty("lawyerProfileId").GetGuid());
    }

    public static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            userNameOrEmail = identifier,
            password
        }, cancellationToken);
    }

    public static async Task<string> LoginAndReadTokenAsync(
        HttpClient client,
        string identifier,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await LoginAsync(client, identifier, password, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .GetProperty("accessToken").GetString()!;
    }

    public static async Task<string> LoginAndChangeAdminPasswordAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var restrictedToken = await LoginAndReadTokenAsync(
            client,
            "superadmin",
            "InitialPassword1",
            cancellationToken);
        UseToken(client, restrictedToken);
        using var change = await client.PostAsJsonAsync("/api/v1/auth/change-password", new
        {
            currentPassword = "InitialPassword1",
            newPassword = "ChangedAdminPassword2"
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        return (await change.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
            .GetProperty("accessToken").GetString()!;
    }

    public static void UseToken(HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public static async Task<JsonElement> GetJsonAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    }

    public static async Task AssertProblemCodeAsync(
        HttpResponseMessage response,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Contains(
            problem.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == expectedCode);
    }

    public static async Task AssertProblemCodeEndsWithAsync(
        HttpResponseMessage response,
        string expectedCode,
        CancellationToken cancellationToken)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Contains(
            problem.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString()?.EndsWith(expectedCode, StringComparison.Ordinal) == true);
    }

    internal sealed record RegisteredClient(Guid UserAccountId, Guid ClientProfileId);

    internal sealed record RegisteredLawyer(Guid UserAccountId, Guid LawyerProfileId);
}
