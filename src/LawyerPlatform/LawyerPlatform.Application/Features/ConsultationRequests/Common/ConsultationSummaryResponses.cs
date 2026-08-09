namespace LawyerPlatform.Application.Features.ConsultationRequests.Common;

public sealed record ConsultationLawyerSummaryResponse(
    Guid Id,
    string FullName,
    string? ProfessionalTitle);

public sealed record ConsultationReferenceSummaryResponse(
    int Id,
    string NameAr,
    string NameEn);
