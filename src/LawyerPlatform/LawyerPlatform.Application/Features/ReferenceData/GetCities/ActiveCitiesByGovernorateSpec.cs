using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ReferenceData.GetCities;

internal sealed class ActiveCitiesByGovernorateSpec : Specification<City, ReferenceDataResponse>
{
    public ActiveCitiesByGovernorateSpec(int governorateId)
    {
        AddCriteria(city => city.GovernorateId == governorateId && city.IsActive && city.Governorate.IsActive);
        AddOrderBy(city => city.DisplayOrder);
        AddOrderBy(city => city.Id);
        Select(city => new ReferenceDataResponse(city.Id, city.NameAr, city.NameEn));
        UseNoTracking();
    }
}
