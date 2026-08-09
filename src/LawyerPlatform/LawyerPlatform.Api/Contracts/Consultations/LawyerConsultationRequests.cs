using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Api.Contracts.Consultations;

public sealed record UpdateConsultationStatusRequest(
    ConsultationRequestStatus Status,
    string? Reason,
    string RowVersion);
