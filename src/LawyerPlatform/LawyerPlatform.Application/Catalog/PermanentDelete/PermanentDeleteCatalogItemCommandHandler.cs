using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.PermanentDelete;

internal sealed class PermanentDeleteCatalogItemCommandHandler(
    ISoftDeletedWriteRepository<CatalogItem, LawyerPlatformWritePersistence> deletedRepository,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork)
    : ICommandHandler<PermanentDeleteCatalogItemCommand>
{
    public async Task<Result> Handle(
        PermanentDeleteCatalogItemCommand request,
        CancellationToken cancellationToken)
    {
        var item = await deletedRepository.GetDeletedTrackedByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("CatalogItems.DeletedNotFound", "The deleted catalog item was not found.");
        }

        deletedRepository.PermanentDelete(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
