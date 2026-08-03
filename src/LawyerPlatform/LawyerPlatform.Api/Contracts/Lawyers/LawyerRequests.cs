namespace LawyerPlatform.Api.Contracts.Lawyers;

public sealed record UpdateLawyerProfileRequest(
    string FullName,
    string? ProfessionalTitle,
    string? Biography,
    int? YearsOfExperience,
    string? ProfessionalRegistrationNumber,
    string RowVersion);

public sealed record UpsertLawyerOfficeRequest(
    int GovernorateId,
    int CityId,
    int AreaId,
    string DetailedAddress,
    string? PublicPhoneNumber,
    string? RowVersion);

public sealed record ReplaceLawyerSpecializationsRequest(
    IReadOnlyList<int> SpecializationIds,
    string RowVersion);

public sealed class UpdateLawyerProfileImageRequest
{
    public required IFormFile Image { get; init; }
    public required string RowVersion { get; init; }
}

public sealed class UploadLawyerDocumentRequest
{
    public required string DocumentType { get; init; }
    public required IFormFile File { get; init; }
}
