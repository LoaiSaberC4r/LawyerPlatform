using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using LawyerPlatform.Application.Catalog.Specifications;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.GetPage;

internal sealed class GetCatalogItemsPageQueryHandler(
    IReadRepository<CatalogItem, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetCatalogItemsPageQuery, PagedResult<CatalogItemResponse>>
{
    public async Task<Result<PagedResult<CatalogItemResponse>>> Handle(
        GetCatalogItemsPageQuery request,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await repository.ListWithLongCountAsync(
            new CatalogItemsPageSpecification(request.SearchText, request.PageNumber, request.PageSize),
            cancellationToken);

        return Result<PagedResult<CatalogItemResponse>>.Ok(
            new PagedResult<CatalogItemResponse>(
                request.PageNumber,
                request.PageSize,
                totalCount,
                items));
    }
}
