using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Application.Catalog.Restore;

internal sealed class RestoreCatalogItemCommandHandler(
    ISoftDeletedWriteRepository<CatalogItem, LawyerPlatformWritePersistence> deletedRepository,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork)
    : ICommandHandler<RestoreCatalogItemCommand>
{
    public async Task<Result> Handle(RestoreCatalogItemCommand request, CancellationToken cancellationToken)
    {
        var item = await deletedRepository.GetDeletedTrackedByIdAsync(request.Id, cancellationToken);
        if (item is null)
        {
            return Result.NotFound("CatalogItems.DeletedNotFound", "The deleted catalog item was not found.");
        }

        var restoreResult = item.Restore();
        if (restoreResult.IsFailure)
        {
            return restoreResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
