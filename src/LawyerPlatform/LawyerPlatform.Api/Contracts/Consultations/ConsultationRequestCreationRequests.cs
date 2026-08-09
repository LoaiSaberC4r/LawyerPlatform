namespace LawyerPlatform.Api.Contracts.Consultations;

public sealed record CreateGuestConsultationRequest(
    Guid LawyerId,
    int? LegalSpecializationId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string Description,
    DateTime? PreferredAppointmentOnUtc);

public sealed record CreateClientConsultationRequest(
    Guid LawyerId,
    int? LegalSpecializationId,
    string Description,
    DateTime? PreferredAppointmentOnUtc);
