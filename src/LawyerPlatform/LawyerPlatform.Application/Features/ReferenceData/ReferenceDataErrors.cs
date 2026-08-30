using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Application.Features.ReferenceData;

internal static class ReferenceDataErrors
{
    public static Error GovernorateNotFound => Error.NotFound("Location.GovernorateNotFound", ErrorMessage.GovernorateNotFound);
    public static Error CityNotFound => Error.NotFound("Location.CityNotFound", ErrorMessage.CityNotFound);
    public static Error InvalidHierarchy => Error.Validation("Location.InvalidHierarchy", ErrorMessage.InvalidLocationHierarchy);
}
