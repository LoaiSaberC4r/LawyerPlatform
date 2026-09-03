namespace LawyerPlatform.Application.Abstractions.Dashboards;

public sealed record RequestDashboardStatistics(
    long Total,
    long New,
    long UnderReview,
    long Approved,
    long Rejected,
    long Completed);

public sealed record AdminLawyerDashboardStatistics(
    long Total,
    long PendingApproval,
    long Approved,
    long Suspended);

public sealed record AdminDashboardStatistics(
    AdminLawyerDashboardStatistics Lawyers,
    long ClientsTotal,
    RequestDashboardStatistics ConsultationRequests);

public interface IDashboardStatisticsReader
{
    Task<AdminDashboardStatistics> ReadAdminAsync(CancellationToken cancellationToken = default);

    Task<RequestDashboardStatistics?> ReadLawyerAsync(
        Guid userAccountId,
        CancellationToken cancellationToken = default);

    Task<RequestDashboardStatistics?> ReadClientAsync(
        Guid userAccountId,
        CancellationToken cancellationToken = default);
}
