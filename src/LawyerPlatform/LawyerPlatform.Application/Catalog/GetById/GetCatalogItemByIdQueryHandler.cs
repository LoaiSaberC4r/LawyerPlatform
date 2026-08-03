using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Catalog.Specifications;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.GetById;

internal sealed class GetCatalogItemByIdQueryHandler(
    IReadRepository<CatalogItem, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetCatalogItemByIdQuery, CatalogItemResponse>
{
    public async Task<Result<CatalogItemResponse>> Handle(
        GetCatalogItemByIdQuery request,
        CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(
            new CatalogItemByIdSpecification(request.Id),
            cancellationToken);

        return item is null
            ? Result.NotFound<CatalogItemResponse>("CatalogItems.NotFound", "The catalog item was not found.")
            : Result<CatalogItemResponse>.Ok(item);
    }
}
