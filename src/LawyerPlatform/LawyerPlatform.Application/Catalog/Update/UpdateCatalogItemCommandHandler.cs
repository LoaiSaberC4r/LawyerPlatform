using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.Update;

internal sealed class UpdateCatalogItemCommandHandler(
    IWriteRepository<CatalogItem, LawyerPlatformWritePersistence> repository,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork)
    : ICommandHandler<UpdateCatalogItemCommand, CatalogItemResponse>
{
    public async Task<Result<CatalogItemResponse>> Handle(
        UpdateCatalogItemCommand request,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound<CatalogItemResponse>(
                "CatalogItems.NotFound",
                "The catalog item was not found.");
        }

        var updateResult = item.Update(request.Name, request.Description, request.Price);
        if (updateResult.IsFailure)
        {
            return Result<CatalogItemResponse>.Fail(updateResult.Errors);
        }

        repository.Update(item);
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
