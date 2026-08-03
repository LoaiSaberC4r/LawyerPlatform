using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.Delete;

internal sealed class DeleteCatalogItemCommandHandler(
    IWriteRepository<CatalogItem, LawyerPlatformWritePersistence> repository,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork)
    : ICommandHandler<DeleteCatalogItemCommand>
{
    public async Task<Result> Handle(DeleteCatalogItemCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("CatalogItems.NotFound", "The catalog item was not found.");
        }

        repository.Delete(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
