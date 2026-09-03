using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Dashboards;
using LawyerPlatform.Application.Features.Dashboards;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.Dashboard;

public sealed record GetLawyerDashboardQuery : IQuery<RequestDashboardResponse>;

internal sealed class GetLawyerDashboardQueryHandler(
    ICurrentUser currentUser,
    IDashboardStatisticsReader statisticsReader)
    : IQueryHandler<GetLawyerDashboardQuery, RequestDashboardResponse>
{
    public async Task<Result<RequestDashboardResponse>> Handle(
        GetLawyerDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<RequestDashboardResponse>.Fail(LawyerErrors.NotFound);
        }

        var statistics = await statisticsReader.ReadLawyerAsync(userId, cancellationToken);
        if (statistics is null)
        {
            return Result<RequestDashboardResponse>.Fail(LawyerErrors.NotFound);
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
