using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.Specifications;

internal sealed class CatalogItemByIdSpecification : Specification<CatalogItem, CatalogItemResponse>
{
    public CatalogItemByIdSpecification(Guid id)
    {
        AddCriteria(item => item.Id == id);
        UseNoTracking();
        Select(item => new CatalogItemResponse(
            item.Id,
            item.Name,
            item.Description,
            item.Price,
            item.CreatedOnUtc,
            item.ModifiedOnUtc,
            item.RowVersion));
    }
}
