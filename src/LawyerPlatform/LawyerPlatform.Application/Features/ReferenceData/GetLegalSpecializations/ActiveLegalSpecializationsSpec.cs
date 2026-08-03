using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ReferenceData.GetLegalSpecializations;

internal sealed class ActiveLegalSpecializationsSpec : Specification<LegalSpecialization, ReferenceDataResponse>
{
    public ActiveLegalSpecializationsSpec()
    {
        AddCriteria(specialization => specialization.IsActive);
        AddOrderBy(specialization => specialization.DisplayOrder);
        AddOrderBy(specialization => specialization.Id);
        Select(specialization => new ReferenceDataResponse(specialization.Id, specialization.NameAr, specialization.NameEn));
        UseNoTracking();
    }
}
