namespace LawyerPlatform.Api.Contracts.ReferenceData;

public sealed record CreateLegalSpecializationRequest(string NameAr, string NameEn, int DisplayOrder);

public sealed record UpdateLegalSpecializationRequest(
    string NameAr,
    string NameEn,
    int DisplayOrder,
    string RowVersion);

public sealed record ChangeReferenceDataStatusRequest(string RowVersion);
