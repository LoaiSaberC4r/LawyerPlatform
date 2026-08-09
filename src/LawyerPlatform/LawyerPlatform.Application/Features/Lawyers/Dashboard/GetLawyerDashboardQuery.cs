using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Features.Dashboards;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.Dashboard;

public sealed record GetLawyerDashboardQuery : IQuery<RequestDashboardResponse>;

internal sealed class GetLawyerDashboardQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> lawyerRepository,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> requestRepository)
    : IQueryHandler<GetLawyerDashboardQuery, RequestDashboardResponse>
{
    public async Task<Result<RequestDashboardResponse>> Handle(
        GetLawyerDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId ||
            !await lawyerRepository.AnyAsync(profile => profile.UserAccountId == userId, cancellationToken))
        {
            return Result<RequestDashboardResponse>.Fail(LawyerErrors.NotFound);
        }

        var total = await requestRepository.LongCountAsync(
            request => request.LawyerProfile.UserAccountId == userId,
            cancellationToken);
        var newCount = await CountStatusAsync(userId, ConsultationRequestStatus.New, cancellationToken);
        var underReview = await CountStatusAsync(userId, ConsultationRequestStatus.UnderReview, cancellationToken);
        var approved = await CountStatusAsync(userId, ConsultationRequestStatus.Approved, cancellationToken);
        var rejected = await CountStatusAsync(userId, ConsultationRequestStatus.Rejected, cancellationToken);
        var completed = await CountStatusAsync(userId, ConsultationRequestStatus.Completed, cancellationToken);

        return Result<RequestDashboardResponse>.Ok(new RequestDashboardResponse(
            total,
            newCount,
            underReview,
            approved,
            rejected,
            completed));
    }

    private Task<long> CountStatusAsync(
        Guid userId,
        ConsultationRequestStatus status,
        CancellationToken cancellationToken)
        => requestRepository.LongCountAsync(
            request => request.LawyerProfile.UserAccountId == userId && request.Status == status,
            cancellationToken);
}
