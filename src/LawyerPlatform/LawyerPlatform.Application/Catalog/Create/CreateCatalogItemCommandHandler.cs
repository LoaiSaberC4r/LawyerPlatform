using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.Create;

internal sealed class CreateCatalogItemCommandHandler(
    IWriteRepository<CatalogItem, LawyerPlatformWritePersistence> repository,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork)
    : ICommandHandler<CreateCatalogItemCommand, CatalogItemResponse>
{
    public async Task<Result<CatalogItemResponse>> Handle(
        CreateCatalogItemCommand request,
        CancellationToken cancellationToken)
    {
        var createResult = CatalogItem.Create(
            request.Name,
            request.Description,
            request.Price);

        if (createResult.IsFailure)
        {
            return Result<CatalogItemResponse>.Fail(createResult.Errors);
        }

        var item = createResult.Value;
        await repository.AddAsync(item, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CatalogItemResponse>.Ok(new CatalogItemResponse(
            item.Id,
            item.Name,
            item.Description,
            item.Price,
            item.CreatedOnUtc,
            item.ModifiedOnUtc,
            item.RowVersion));
    }
}
