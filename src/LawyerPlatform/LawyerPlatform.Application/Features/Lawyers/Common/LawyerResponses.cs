namespace LawyerPlatform.Application.Features.Lawyers.Common;

public sealed record LawyerProfileCompletionResponse(
    bool ProfessionalProfileComplete,
    bool OfficeComplete,
    bool SpecializationsComplete,
    bool DocumentsComplete,
    bool ProfileIsComplete,
    bool CanSubmitForApproval,
    IReadOnlyList<string> MissingRequirements);

public sealed record LawyerOfficeResponse(
    Guid Id,
    int GovernorateId,
    string GovernorateNameAr,
    string GovernorateNameEn,
    int CityId,
    string CityNameAr,
    string CityNameEn,
    int AreaId,
    string AreaNameAr,
    string AreaNameEn,
    string DetailedAddress,
    string? PublicPhoneNumber,
    decimal? Latitude,
    decimal? Longitude,
    string RowVersion);

public sealed record LawyerSpecializationResponse(
    int Id,
    string NameAr,
    string NameEn);

public sealed record LawyerDocumentResponse(
    Guid Id,
    string DocumentType,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    DateTime UploadedOnUtc,
    string ContentUrl,
    string RowVersion);

public sealed record StoredFileResponse(
    Stream Content,
    string ContentType,
    string FileName,
    long Length);
