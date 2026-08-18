namespace LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.Common;

public sealed record ConsultationSpecializationResponse(int Id, string NameAr, string NameEn);

public sealed record ConsultationStatusHistoryResponse(
    Guid Id,
    string OldStatus,
    string NewStatus,
    Guid ChangedByUserId,
    string? Reason,
    DateTime ChangedOnUtc);

public sealed record LawyerConsultationListItemResponse(
    Guid Id,
    string ReferenceNumber,
    string RequesterType,
    string RequesterFullName,
    ConsultationSpecializationResponse? LegalSpecialization,
    string ConsultationType,
    decimal? ConsultationPrice,
    string Status,
    DateTime? PreferredAppointmentOnUtc,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    string RowVersion);

public sealed record LawyerConsultationDetailsResponse(
    Guid Id,
    string ReferenceNumber,
    string RequesterType,
    string RequesterFullName,
    string RequesterPhoneNumber,
    string? RequesterEmail,
    ConsultationSpecializationResponse? LegalSpecialization,
    string Description,
    string ConsultationType,
    DateTime? PreferredAppointmentOnUtc,
    decimal? ConsultationPrice,
    string Status,
    string? RejectionReason,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    DateTime? CompletedOnUtc,
    IReadOnlyList<ConsultationStatusHistoryResponse> StatusHistory,
    string RowVersion);
