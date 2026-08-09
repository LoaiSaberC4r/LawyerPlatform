using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Features.Dashboards;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.Clients.Dashboard;

public sealed record GetClientDashboardQuery : IQuery<RequestDashboardResponse>;

internal sealed class GetClientDashboardQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<ClientProfile, LawyerPlatformReadPersistence> clientRepository,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> requestRepository)
    : IQueryHandler<GetClientDashboardQuery, RequestDashboardResponse>
{
    public async Task<Result<RequestDashboardResponse>> Handle(
        GetClientDashboardQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId ||
            !await clientRepository.AnyAsync(profile => profile.UserAccountId == userId, cancellationToken))
        {
            return Result<RequestDashboardResponse>.Fail(ClientErrors.NotFound);
        }

        var total = await requestRepository.LongCountAsync(
            request => request.ClientProfile != null && request.ClientProfile.UserAccountId == userId,
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
            request => request.ClientProfile != null &&
                       request.ClientProfile.UserAccountId == userId &&
                       request.Status == status,
            cancellationToken);
}
