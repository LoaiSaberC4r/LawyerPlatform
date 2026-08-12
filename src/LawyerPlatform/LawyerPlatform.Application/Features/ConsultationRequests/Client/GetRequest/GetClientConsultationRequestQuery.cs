using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.ConsultationRequests.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Client.GetRequest;

public sealed record GetClientConsultationRequestQuery(Guid RequestId)
    : IQuery<ClientConsultationDetailsResponse>;

public sealed record ClientConsultationDetailsResponse(
    Guid Id,
    string ReferenceNumber,
    ConsultationLawyerSummaryResponse Lawyer,
    ConsultationReferenceSummaryResponse? LegalSpecialization,
    string Description,
    DateTime? PreferredAppointmentOnUtc,
    decimal? ConsultationPrice,
    string Status,
    string? RejectionReason,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    DateTime? CompletedOnUtc);

internal sealed class GetClientConsultationRequestQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetClientConsultationRequestQuery, ClientConsultationDetailsResponse>
{
    public async Task<Result<ClientConsultationDetailsResponse>> Handle(
        GetClientConsultationRequestQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<ClientConsultationDetailsResponse>.Fail(ConsultationRequestErrors.NotFound);
        }

        var item = await repository.FirstOrDefaultAsync(
            new ClientConsultationRequestDetailsSpecification(userId, query.RequestId),
            cancellationToken);
        return item is null
            ? Result<ClientConsultationDetailsResponse>.Fail(ConsultationRequestErrors.NotFound)
            : Result<ClientConsultationDetailsResponse>.Ok(item);
    }
}

internal sealed class ClientConsultationRequestDetailsSpecification
    : Specification<ConsultationRequest, ClientConsultationDetailsResponse>
{
    public ClientConsultationRequestDetailsSpecification(Guid userAccountId, Guid requestId)
    {
        AddCriteria(request =>
            request.Id == requestId &&
            request.ClientProfile != null &&
            request.ClientProfile.UserAccountId == userAccountId);
        UseNoTracking();
        Select(request => new ClientConsultationDetailsResponse(
            request.Id,
            request.ReferenceNumber,
            new ConsultationLawyerSummaryResponse(
                request.LawyerProfileId,
                request.LawyerProfile.FullName,
                request.LawyerProfile.ProfessionalTitle),
            request.LegalSpecialization == null
                ? null
                : new ConsultationReferenceSummaryResponse(
                    request.LegalSpecialization.Id,
                    request.LegalSpecialization.NameAr,
                    request.LegalSpecialization.NameEn),
            request.Description,
            request.PreferredAppointmentOnUtc,
            request.ConsultationPrice,
            request.Status.ToString(),
            request.Status == ConsultationRequestStatus.Rejected
                ? request.StatusHistory
                    .Where(history => history.NewStatus == ConsultationRequestStatus.Rejected)
                    .OrderByDescending(history => history.ChangedOnUtc)
                    .Select(history => history.Reason)
                    .FirstOrDefault()
                : null,
            request.CreatedOnUtc,
            request.ModifiedOnUtc,
            request.CompletedOnUtc));
    }
}
