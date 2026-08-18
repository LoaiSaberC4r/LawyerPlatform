using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.GetRequest;

public sealed record GetLawyerConsultationRequestQuery(Guid RequestId)
    : IQuery<LawyerConsultationDetailsResponse>;

internal sealed class GetLawyerConsultationRequestQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<ConsultationRequest, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetLawyerConsultationRequestQuery, LawyerConsultationDetailsResponse>
{
    public async Task<Result<LawyerConsultationDetailsResponse>> Handle(
        GetLawyerConsultationRequestQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<LawyerConsultationDetailsResponse>.Fail(ConsultationRequestErrors.NotFound);
        }

        var item = await repository.FirstOrDefaultAsync(
            new LawyerConsultationRequestDetailsSpecification(userId, query.RequestId),
            cancellationToken);
        return item is null
            ? Result<LawyerConsultationDetailsResponse>.Fail(ConsultationRequestErrors.NotFound)
            : Result<LawyerConsultationDetailsResponse>.Ok(item.ToResponse());
    }
}

internal sealed record LawyerConsultationHistorySnapshot(
    Guid Id,
    ConsultationRequestStatus OldStatus,
    ConsultationRequestStatus NewStatus,
    Guid ChangedByUserId,
    string? Reason,
    DateTime ChangedOnUtc);

internal sealed record LawyerConsultationDetailsSnapshot(
    Guid Id,
    string ReferenceNumber,
    bool IsClient,
    string RequesterFullName,
    string RequesterPhoneNumber,
    string? RequesterEmail,
    int? SpecializationId,
    string? SpecializationNameAr,
    string? SpecializationNameEn,
    string Description,
    ConsultationType ConsultationType,
    DateTime? PreferredAppointmentOnUtc,
    decimal? ConsultationPrice,
    ConsultationRequestStatus Status,
    string? RejectionReason,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    DateTime? CompletedOnUtc,
    IReadOnlyList<LawyerConsultationHistorySnapshot> History,
    byte[] RowVersion)
{
    public LawyerConsultationDetailsResponse ToResponse()
        => new(
            Id,
            ReferenceNumber,
            IsClient ? "Client" : "Guest",
            RequesterFullName,
            RequesterPhoneNumber,
            RequesterEmail,
            SpecializationId.HasValue
                ? new ConsultationSpecializationResponse(
                    SpecializationId.Value,
                    SpecializationNameAr!,
                    SpecializationNameEn!)
                : null,
            Description,
            ConsultationType.ToString(),
            PreferredAppointmentOnUtc,
            ConsultationPrice,
            Status.ToString(),
            RejectionReason,
            CreatedOnUtc,
            ModifiedOnUtc,
            CompletedOnUtc,
            History.Select(item => new ConsultationStatusHistoryResponse(
                item.Id,
                item.OldStatus.ToString(),
                item.NewStatus.ToString(),
                item.ChangedByUserId,
                item.Reason,
                item.ChangedOnUtc)).ToArray(),
            RowVersionCodec.Encode(RowVersion));
}

internal sealed class LawyerConsultationRequestDetailsSpecification
    : Specification<ConsultationRequest, LawyerConsultationDetailsSnapshot>
{
    public LawyerConsultationRequestDetailsSpecification(Guid userAccountId, Guid requestId)
    {
        AddCriteria(request =>
            request.Id == requestId && request.LawyerProfile.UserAccountId == userAccountId);
        UseNoTracking();
        UseSplitQuery();
        Select(request => new LawyerConsultationDetailsSnapshot(
            request.Id,
            request.ReferenceNumber,
            request.ClientProfileId != null,
            request.ClientProfileId != null ? request.ClientProfile!.FullName : request.GuestFullName!,
            request.ClientProfileId != null ? request.ClientProfile!.UserAccount.PhoneNumber : request.GuestPhoneNumber!,
            request.ClientProfileId != null ? request.ClientProfile!.UserAccount.Email : request.GuestEmail,
            request.LegalSpecializationId,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameAr : null,
            request.LegalSpecialization != null ? request.LegalSpecialization.NameEn : null,
            request.Description,
            request.ConsultationType,
            request.PreferredAppointmentOnUtc,
            request.ConsultationPrice,
            request.Status,
            request.StatusHistory
                .Where(history => history.NewStatus == ConsultationRequestStatus.Rejected)
                .OrderByDescending(history => history.ChangedOnUtc)
                .Select(history => history.Reason)
                .FirstOrDefault(),
            request.CreatedOnUtc,
            request.ModifiedOnUtc,
            request.CompletedOnUtc,
            request.StatusHistory
                .OrderBy(history => history.ChangedOnUtc)
                .ThenBy(history => history.Id)
                .Select(history => new LawyerConsultationHistorySnapshot(
                    history.Id,
                    history.OldStatus,
                    history.NewStatus,
                    history.ChangedByUserId,
                    history.Reason,
                    history.ChangedOnUtc))
                .ToArray(),
            request.RowVersion));
    }
}
