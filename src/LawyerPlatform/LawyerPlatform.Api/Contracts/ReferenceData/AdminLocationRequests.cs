namespace LawyerPlatform.Api.Contracts.ReferenceData;

public sealed record CreateGovernorateRequest(string NameAr, string NameEn, int DisplayOrder);
public sealed record UpdateGovernorateRequest(string NameAr, string NameEn, int DisplayOrder, string RowVersion);
public sealed record CreateCityRequest(int GovernorateId, string NameAr, string NameEn, int DisplayOrder);
public sealed record UpdateCityRequest(int GovernorateId, string NameAr, string NameEn, int DisplayOrder, string RowVersion);
public sealed record CreateAreaRequest(int CityId, string NameAr, string NameEn, int DisplayOrder);
public sealed record UpdateAreaRequest(int CityId, string NameAr, string NameEn, int DisplayOrder, string RowVersion);
