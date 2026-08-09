using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;

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
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> lawyerRepository,
    IReadRepository<ClientProfile, LawyerPlatformReadPersistence> clientRepository,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> requestRepository)
    : IQueryHandler<GetAdminDashboardQuery, AdminDashboardResponse>
{
    public async Task<Result<AdminDashboardResponse>> Handle(
        GetAdminDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var lawyersTotal = await lawyerRepository.LongCountAsync(cancellationToken: cancellationToken);
        var pendingLawyers = await lawyerRepository.LongCountAsync(
            profile => profile.ApprovalStatus == LawyerApprovalStatus.PendingApproval,
            cancellationToken);
        var approvedLawyers = await lawyerRepository.LongCountAsync(
            profile => profile.ApprovalStatus == LawyerApprovalStatus.Approved,
            cancellationToken);
        var suspendedLawyers = await lawyerRepository.LongCountAsync(
            profile => profile.ApprovalStatus == LawyerApprovalStatus.Suspended,
            cancellationToken);
        var clientsTotal = await clientRepository.LongCountAsync(cancellationToken: cancellationToken);
        var requestsTotal = await requestRepository.LongCountAsync(cancellationToken: cancellationToken);
        var newRequests = await CountStatusAsync(ConsultationRequestStatus.New, cancellationToken);
        var underReviewRequests = await CountStatusAsync(ConsultationRequestStatus.UnderReview, cancellationToken);
        var approvedRequests = await CountStatusAsync(ConsultationRequestStatus.Approved, cancellationToken);
        var rejectedRequests = await CountStatusAsync(ConsultationRequestStatus.Rejected, cancellationToken);
        var completedRequests = await CountStatusAsync(ConsultationRequestStatus.Completed, cancellationToken);

        return Result<AdminDashboardResponse>.Ok(new AdminDashboardResponse(
            new AdminLawyerDashboardResponse(
                lawyersTotal,
                pendingLawyers,
                approvedLawyers,
                suspendedLawyers),
            new AdminClientDashboardResponse(clientsTotal),
            new AdminConsultationDashboardResponse(
                requestsTotal,
                new AdminConsultationStatusDashboardResponse(
                    newRequests,
                    underReviewRequests,
                    approvedRequests,
                    rejectedRequests,
                    completedRequests))));
    }

    private Task<long> CountStatusAsync(
        ConsultationRequestStatus status,
        CancellationToken cancellationToken)
        => requestRepository.LongCountAsync(request => request.Status == status, cancellationToken);
}
