using LawyerPlatform.Application.Abstractions.Dashboards;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LawyerPlatform.IntegrationTests;

public sealed class DashboardStatisticsReaderTests
{
    [Fact]
    public async Task AggregatesExactCountsOwnershipZeroStateMissingProfilesAndBoundedQueries()
    {
        await using var factory = new CustomWebApplicationFactory();
        _ = factory.Services;
        var nowUtc = DateTime.UtcNow;
        var clientAccount = CreateAccount(AccountRole.Client, "dashboard.exact.client", "01070000001", nowUtc);
        var lawyerAccount = CreateAccount(AccountRole.Lawyer, "dashboard.exact.lawyer", "01070000002", nowUtc);
        var emptyClientAccount = CreateAccount(AccountRole.Client, "dashboard.zero.client", "01070000003", nowUtc);
        var emptyLawyerAccount = CreateAccount(AccountRole.Lawyer, "dashboard.zero.lawyer", "01070000004", nowUtc);
        var client = ClientProfile.Create(clientAccount, "Exact Dashboard Client").Value;
        var lawyer = LawyerProfile.Create(lawyerAccount, "Exact Dashboard Lawyer").Value;
        var emptyClient = ClientProfile.Create(emptyClientAccount, "Zero Dashboard Client").Value;
        var emptyLawyer = LawyerProfile.Create(emptyLawyerAccount, "Zero Dashboard Lawyer").Value;

        var requests = new List<ConsultationRequest>();
        AddRequests(requests, 3, ConsultationRequestStatus.New);
        AddRequests(requests, 2, ConsultationRequestStatus.UnderReview);
        AddRequests(requests, 4, ConsultationRequestStatus.Approved);
        AddRequests(requests, 1, ConsultationRequestStatus.Rejected);
        AddRequests(requests, 10, ConsultationRequestStatus.Completed);

        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<LawyerPlatformDbContext>();
            context.AddRange(
                clientAccount,
                lawyerAccount,
                emptyClientAccount,
                emptyLawyerAccount,
                client,
                lawyer,
                emptyClient,
                emptyLawyer);
            context.ConsultationRequests.AddRange(requests);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IDashboardStatisticsReader>();
        var counter = scope.ServiceProvider.GetRequiredService<TestDbCommandCounter>();

        counter.Reset();
        AssertExact(await reader.ReadClientAsync(clientAccount.Id, TestContext.Current.CancellationToken));
        Assert.True(counter.Commands.Count <= 2);

        counter.Reset();
        AssertExact(await reader.ReadLawyerAsync(lawyerAccount.Id, TestContext.Current.CancellationToken));
        Assert.True(counter.Commands.Count <= 2);

        counter.Reset();
        var admin = await reader.ReadAdminAsync(TestContext.Current.CancellationToken);
        AssertExact(admin.ConsultationRequests);
        Assert.Equal(2, admin.ClientsTotal);
        Assert.Equal(2, admin.Lawyers.Total);
        Assert.True(counter.Commands.Count <= 3);

        AssertZero(await reader.ReadClientAsync(emptyClientAccount.Id, TestContext.Current.CancellationToken));
        AssertZero(await reader.ReadLawyerAsync(emptyLawyerAccount.Id, TestContext.Current.CancellationToken));
        Assert.Null(await reader.ReadClientAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Null(await reader.ReadLawyerAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        void AddRequests(
            ICollection<ConsultationRequest> destination,
            int count,
            ConsultationRequestStatus status)
        {
            for (var index = 0; index < count; index++)
            {
                var request = ConsultationRequest.CreateForClient(
                    $"DASH-{status}-{index}",
                    client.Id,
                    lawyer.Id,
                    ConsultationType.Online,
                    null,
                    "Dashboard aggregation test",
                    null,
                    nowUtc,
                    500m).Value;
                MoveToStatus(request, status, clientAccount.Id, nowUtc);
                destination.Add(request);
            }
        }
    }

    private static UserAccount CreateAccount(
        AccountRole role,
        string userName,
        string phoneNumber,
        DateTime nowUtc)
    {
        var normalized = userName.ToUpperInvariant();
        var email = $"{userName}@example.test";
        return role == AccountRole.Client
            ? UserAccount.CreateClient(
                userName,
                normalized,
                email,
                email.ToUpperInvariant(),
                phoneNumber,
                "integration-test-hash",
                nowUtc).Value
            : UserAccount.CreateLawyer(
                userName,
                normalized,
                email,
                email.ToUpperInvariant(),
                phoneNumber,
                "integration-test-hash",
                nowUtc).Value;
    }

    private static void MoveToStatus(
        ConsultationRequest request,
        ConsultationRequestStatus status,
        Guid actorId,
        DateTime nowUtc)
    {
        if (status == ConsultationRequestStatus.New)
        {
            return;
        }

        if (status == ConsultationRequestStatus.Completed)
        {
            Assert.True(request.ChangeStatus(
                ConsultationRequestStatus.Approved,
                actorId,
                null,
                nowUtc).IsSuccess);
        }

        Assert.True(request.ChangeStatus(
            status,
            actorId,
            status == ConsultationRequestStatus.Rejected ? "Rejected for aggregation test" : null,
            nowUtc).IsSuccess);
    }

    private static void AssertExact(RequestDashboardStatistics? statistics)
    {
        Assert.NotNull(statistics);
        Assert.Equal(20, statistics.Total);
        Assert.Equal(3, statistics.New);
        Assert.Equal(2, statistics.UnderReview);
        Assert.Equal(4, statistics.Approved);
        Assert.Equal(1, statistics.Rejected);
        Assert.Equal(10, statistics.Completed);
    }

    private static void AssertZero(RequestDashboardStatistics? statistics)
    {
        Assert.NotNull(statistics);
        Assert.Equal(new RequestDashboardStatistics(0, 0, 0, 0, 0, 0), statistics);
    }
}
