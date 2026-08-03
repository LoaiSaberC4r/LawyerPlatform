using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ReferenceData.GetAreas;

internal sealed class ActiveAreasByCitySpec : Specification<Area, ReferenceDataResponse>
{
    public ActiveAreasByCitySpec(int cityId)
    {
        AddCriteria(area => area.CityId == cityId && area.IsActive);
        AddOrderBy(area => area.DisplayOrder);
        AddOrderBy(area => area.Id);
        Select(area => new ReferenceDataResponse(area.Id, area.NameAr, area.NameEn));
        UseNoTracking();
    }
}
