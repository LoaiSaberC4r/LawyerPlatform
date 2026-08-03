using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Application.Features.ReferenceData;

internal static class ReferenceDataErrors
{
    public static readonly Error GovernorateNotFound = Error.NotFound("Location.GovernorateNotFound", "Governorate was not found.");
    public static readonly Error CityNotFound = Error.NotFound("Location.CityNotFound", "City was not found.");
    public static readonly Error InvalidHierarchy = Error.Validation("Location.InvalidHierarchy", "The location hierarchy is invalid or inactive.");
}
