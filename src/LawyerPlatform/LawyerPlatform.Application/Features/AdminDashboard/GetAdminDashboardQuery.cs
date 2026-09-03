using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Dashboards;

namespace LawyerPlatform.Application.Features.AdminDashboard;

public sealed record AdminLawyerDashboardResponse(
    long Total,
    long PendingApproval,
    long Approved,
    long Suspended);

public sealed record AdminClientDashboardResponse(long Total);

public sealed record AdminConsultationStatusDashboardResponse(
    long New,
    long UnderReview,
    long Approved,
    long Rejected,
    long Completed);

public sealed record AdminConsultationDashboardResponse(
    long Total,
    AdminConsultationStatusDashboardResponse ByStatus);

public sealed record AdminDashboardResponse(
    AdminLawyerDashboardResponse Lawyers,
    AdminClientDashboardResponse Clients,
    AdminConsultationDashboardResponse ConsultationRequests);

public sealed record GetAdminDashboardQuery : IQuery<AdminDashboardResponse>;

internal sealed class GetAdminDashboardQueryHandler(
    IDashboardStatisticsReader statisticsReader)
    : IQueryHandler<GetAdminDashboardQuery, AdminDashboardResponse>
{
    public async Task<Result<AdminDashboardResponse>> Handle(
        GetAdminDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var statistics = await statisticsReader.ReadAdminAsync(cancellationToken);

        return Result<AdminDashboardResponse>.Ok(new AdminDashboardResponse(
            new AdminLawyerDashboardResponse(
                statistics.Lawyers.Total,
                statistics.Lawyers.PendingApproval,
                statistics.Lawyers.Approved,
                statistics.Lawyers.Suspended),
            new AdminClientDashboardResponse(statistics.ClientsTotal),
            new AdminConsultationDashboardResponse(
                statistics.ConsultationRequests.Total,
                new AdminConsultationStatusDashboardResponse(
                    statistics.ConsultationRequests.New,
                    statistics.ConsultationRequests.UnderReview,
                    statistics.ConsultationRequests.Approved,
                    statistics.ConsultationRequests.Rejected,
                    statistics.ConsultationRequests.Completed))));
    }
}
