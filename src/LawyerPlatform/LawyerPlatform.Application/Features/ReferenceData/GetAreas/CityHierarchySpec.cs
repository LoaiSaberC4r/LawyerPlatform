using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ReferenceData.GetAreas;

internal sealed record CityHierarchy(bool CityIsActive, bool GovernorateIsActive);

internal sealed class CityHierarchySpec : Specification<City, CityHierarchy>
{
    public CityHierarchySpec(int cityId)
    {
        AddCriteria(city => city.Id == cityId);
        Select(city => new CityHierarchy(city.IsActive, city.Governorate.IsActive));
        UseNoTracking();
    }
}
