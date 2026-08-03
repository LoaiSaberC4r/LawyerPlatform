using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ReferenceData.GetGovernorates;

internal sealed class ActiveGovernoratesSpec : Specification<Governorate, ReferenceDataResponse>
{
    public ActiveGovernoratesSpec()
    {
        AddCriteria(governorate => governorate.IsActive);
        AddOrderBy(governorate => governorate.DisplayOrder);
        AddOrderBy(governorate => governorate.Id);
        Select(governorate => new ReferenceDataResponse(governorate.Id, governorate.NameAr, governorate.NameEn));
        UseNoTracking();
    }
}
