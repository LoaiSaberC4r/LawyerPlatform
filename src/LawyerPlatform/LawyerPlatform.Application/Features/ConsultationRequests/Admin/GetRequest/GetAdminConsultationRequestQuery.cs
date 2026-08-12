using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.ConsultationRequests.Common;
using LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Admin.GetRequest;

public sealed record GetAdminConsultationRequestQuery(Guid RequestId) : IQuery<AdminConsultationDetailsResponse>;

public sealed record AdminConsultationDetailsResponse(
    Guid Id,
    string ReferenceNumber,
    string RequesterType,
    Guid? ClientProfileId,
    string RequesterFullName,
    string RequesterPhoneNumber,
    string? RequesterEmail,
    ConsultationLawyerSummaryResponse Lawyer,
    ConsultationSpecializationResponse? LegalSpecialization,
    string Description,
    DateTime? PreferredAppointmentOnUtc,
    decimal? ConsultationPrice,
    string Status,
    string? RejectionReason,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    DateTime? CompletedOnUtc,
    IReadOnlyList<ConsultationStatusHistoryResponse> StatusHistory,
    string RowVersion);

internal sealed class GetAdminConsultationRequestQueryHandler(
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminConsultationRequestQuery, AdminConsultationDetailsResponse>
{
    public async Task<Result<AdminConsultationDetailsResponse>> Handle(
        GetAdminConsultationRequestQuery query,
        CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(
            new AdminConsultationRequestDetailsSpecification(query.RequestId), cancellationToken);
        return item is null
            ? Result<AdminConsultationDetailsResponse>.Fail(ConsultationRequestErrors.NotFound)
            : Result<AdminConsultationDetailsResponse>.Ok(item.ToResponse());
    }
}

internal sealed record AdminConsultationHistorySnapshot(
    Guid Id, ConsultationRequestStatus OldStatus, ConsultationRequestStatus NewStatus,
    Guid ChangedByUserId, string? Reason, DateTime ChangedOnUtc);

internal sealed record AdminConsultationDetailsSnapshot(
    Guid Id, string ReferenceNumber, Guid? ClientProfileId, string RequesterFullName,
    string RequesterPhoneNumber, string? RequesterEmail,
    Guid LawyerId, string LawyerFullName, string? LawyerProfessionalTitle,
    int? SpecializationId, string? SpecializationNameAr, string? SpecializationNameEn,
    string Description, DateTime? PreferredAppointmentOnUtc, decimal? ConsultationPrice, ConsultationRequestStatus Status,
    string? RejectionReason, DateTime CreatedOnUtc, DateTime? ModifiedOnUtc,
    DateTime? CompletedOnUtc, IReadOnlyList<AdminConsultationHistorySnapshot> History, byte[] RowVersion)
{
    public AdminConsultationDetailsResponse ToResponse() => new(
        Id, ReferenceNumber, ClientProfileId.HasValue ? "Client" : "Guest", ClientProfileId,
        RequesterFullName, RequesterPhoneNumber, RequesterEmail,
        new ConsultationLawyerSummaryResponse(LawyerId, LawyerFullName, LawyerProfessionalTitle),
        SpecializationId.HasValue
            ? new ConsultationSpecializationResponse(SpecializationId.Value, SpecializationNameAr!, SpecializationNameEn!)
            : null,
        Description, PreferredAppointmentOnUtc, ConsultationPrice, Status.ToString(), RejectionReason,
        CreatedOnUtc, ModifiedOnUtc, CompletedOnUtc,
        History.Select(item => new ConsultationStatusHistoryResponse(
            item.Id, item.OldStatus.ToString(), item.NewStatus.ToString(),
            item.ChangedByUserId, item.Reason, item.ChangedOnUtc)).ToArray(),
        RowVersionCodec.Encode(RowVersion));
}

internal sealed class AdminConsultationRequestDetailsSpecification
    : Specification<ConsultationRequest, AdminConsultationDetailsSnapshot>
{
    public AdminConsultationRequestDetailsSpecification(Guid requestId)
    {
        AddCriteria(request => request.Id == requestId);
        UseNoTracking();
        UseSplitQuery();
        Select(request => new AdminConsultationDetailsSnapshot(
            request.Id, request.ReferenceNumber, request.ClientProfileId,
            request.ClientProfileId != null ? request.ClientProfile!.FullName : request.GuestFullName!,
            request.ClientProfileId != null ? request.ClientProfile!.UserAccount.PhoneNumber : request.GuestPhoneNumber!,
            request.ClientProfileId != null ? request.ClientProfile!.UserAccount.Email : request.GuestEmail,
            request.LawyerProfileId, request.LawyerProfile.FullName, request.LawyerProfile.ProfessionalTitle,
            request.LegalSpecializationId,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameAr : null,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameEn : null,
            request.Description, request.PreferredAppointmentOnUtc, request.ConsultationPrice, request.Status,
            request.StatusHistory.Where(history => history.NewStatus == ConsultationRequestStatus.Rejected)
                .OrderByDescending(history => history.ChangedOnUtc).Select(history => history.Reason).FirstOrDefault(),
            request.CreatedOnUtc, request.ModifiedOnUtc, request.CompletedOnUtc,
            request.StatusHistory.OrderBy(history => history.ChangedOnUtc).ThenBy(history => history.Id)
                .Select(history => new AdminConsultationHistorySnapshot(
                    history.Id, history.OldStatus, history.NewStatus,
                    history.ChangedByUserId, history.Reason, history.ChangedOnUtc)).ToArray(),
            request.RowVersion));
    }
}
