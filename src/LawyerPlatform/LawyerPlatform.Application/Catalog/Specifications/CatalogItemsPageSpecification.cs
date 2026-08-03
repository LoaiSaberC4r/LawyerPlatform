using BuildingBlock.Domain.Specification;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.Specifications;

internal sealed class CatalogItemsPageSpecification : Specification<CatalogItem, CatalogItemResponse>
{
    public CatalogItemsPageSpecification(string? searchText, int pageNumber, int pageSize)
    {
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var normalized = searchText.Trim();
            AddCriteria(item =>
                item.Name.Contains(normalized) ||
                (item.Description != null && item.Description.Contains(normalized)));
        }

        AddOrderBy(item => item.Name);
        ApplyPaging(pageNumber, pageSize, maximumPageSize: 100);
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
