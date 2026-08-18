namespace LawyerPlatform.Api.Contracts.Consultations;

public sealed record CreateGuestConsultationRequest(
    Guid LawyerId,
    string? ConsultationType,
    int? LegalSpecializationId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string Description,
    DateTime? PreferredAppointmentOnUtc);

public sealed record CreateClientConsultationRequest(
    Guid LawyerId,
    string? ConsultationType,
    int? LegalSpecializationId,
    string Description,
    DateTime? PreferredAppointmentOnUtc);

internal static class ConsultationTypeContract
{
    public static LawyerPlatform.Domain.Consultations.ConsultationType? Parse(string? value)
        => Enum.TryParse<LawyerPlatform.Domain.Consultations.ConsultationType>(
               value,
               ignoreCase: true,
               out var consultationType) && Enum.IsDefined(consultationType)
            ? consultationType
            : null;
}
