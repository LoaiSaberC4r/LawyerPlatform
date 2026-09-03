using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Dashboards;
using LawyerPlatform.Application.Features.Dashboards;
using LawyerPlatform.Domain.Clients;

namespace LawyerPlatform.Application.Features.Clients.Dashboard;

public sealed record GetClientDashboardQuery : IQuery<RequestDashboardResponse>;

internal sealed class GetClientDashboardQueryHandler(
    ICurrentUser currentUser,
    IDashboardStatisticsReader statisticsReader)
    : IQueryHandler<GetClientDashboardQuery, RequestDashboardResponse>
{
    public async Task<Result<RequestDashboardResponse>> Handle(
        GetClientDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<RequestDashboardResponse>.Fail(ClientErrors.NotFound);
        }

        var statistics = await statisticsReader.ReadClientAsync(userId, cancellationToken);
        if (statistics is null)
        {
            return Result<RequestDashboardResponse>.Fail(ClientErrors.NotFound);
        }

        return Result<RequestDashboardResponse>.Ok(new RequestDashboardResponse(
            statistics.Total,
            statistics.New,
            statistics.UnderReview,
            statistics.Approved,
            statistics.Rejected,
            statistics.Completed));
    }
}
