using LawyerPlatform.Application.Abstractions.Dashboards;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.Infrastructure.Dashboards;

internal sealed class DashboardStatisticsReader(LawyerPlatformDbContext dbContext)
    : IDashboardStatisticsReader
{
    public async Task<AdminDashboardStatistics> ReadAdminAsync(
        CancellationToken cancellationToken = default)
    {
        var lawyerGroups = await dbContext.LawyerProfiles
            .AsNoTracking()
            .GroupBy(profile => profile.ApprovalStatus)
            .Select(group => new StatusCount<LawyerApprovalStatus>(group.Key, group.LongCount()))
            .ToListAsync(cancellationToken);
        var lawyerCounts = lawyerGroups.ToDictionary(item => item.Status, item => item.Count);

        var clientsTotal = await dbContext.ClientProfiles
            .AsNoTracking()
            .LongCountAsync(cancellationToken);

        var requestGroups = await dbContext.ConsultationRequests
            .AsNoTracking()
            .GroupBy(request => request.Status)
            .Select(group => new StatusCount<ConsultationRequestStatus>(group.Key, group.LongCount()))
            .ToListAsync(cancellationToken);

        return new AdminDashboardStatistics(
            new AdminLawyerDashboardStatistics(
                lawyerGroups.Sum(item => item.Count),
                GetCount(lawyerCounts, LawyerApprovalStatus.PendingApproval),
                GetCount(lawyerCounts, LawyerApprovalStatus.Approved),
                GetCount(lawyerCounts, LawyerApprovalStatus.Suspended)),
            clientsTotal,
            ToRequestStatistics(requestGroups));
    }

    public async Task<RequestDashboardStatistics?> ReadLawyerAsync(
        Guid userAccountId,
        CancellationToken cancellationToken = default)
    {
        var lawyerProfileId = await dbContext.LawyerProfiles
            .AsNoTracking()
            .Where(profile => profile.UserAccountId == userAccountId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (lawyerProfileId is null)
        {
            return null;
        }

        var groups = await dbContext.ConsultationRequests
            .AsNoTracking()
            .Where(request => request.LawyerProfileId == lawyerProfileId.Value)
            .GroupBy(request => request.Status)
            .Select(group => new StatusCount<ConsultationRequestStatus>(group.Key, group.LongCount()))
            .ToListAsync(cancellationToken);
        return ToRequestStatistics(groups);
    }

    public async Task<RequestDashboardStatistics?> ReadClientAsync(
        Guid userAccountId,
        CancellationToken cancellationToken = default)
    {
        var clientProfileId = await dbContext.ClientProfiles
            .AsNoTracking()
            .Where(profile => profile.UserAccountId == userAccountId)
            .Select(profile => (Guid?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (clientProfileId is null)
        {
            return null;
        }

        var groups = await dbContext.ConsultationRequests
            .AsNoTracking()
            .Where(request => request.ClientProfileId == clientProfileId.Value)
            .GroupBy(request => request.Status)
            .Select(group => new StatusCount<ConsultationRequestStatus>(group.Key, group.LongCount()))
            .ToListAsync(cancellationToken);
        return ToRequestStatistics(groups);
    }

    private static RequestDashboardStatistics ToRequestStatistics(
        IReadOnlyCollection<StatusCount<ConsultationRequestStatus>> groups)
    {
        var counts = groups.ToDictionary(item => item.Status, item => item.Count);
        return new RequestDashboardStatistics(
            groups.Sum(item => item.Count),
            GetCount(counts, ConsultationRequestStatus.New),
            GetCount(counts, ConsultationRequestStatus.UnderReview),
            GetCount(counts, ConsultationRequestStatus.Approved),
            GetCount(counts, ConsultationRequestStatus.Rejected),
            GetCount(counts, ConsultationRequestStatus.Completed));
    }

    private static long GetCount<TStatus>(
        IReadOnlyDictionary<TStatus, long> counts,
        TStatus status)
        where TStatus : struct, Enum
        => counts.GetValueOrDefault(status);

    private sealed record StatusCount<TStatus>(TStatus Status, long Count)
        where TStatus : struct, Enum;
}
