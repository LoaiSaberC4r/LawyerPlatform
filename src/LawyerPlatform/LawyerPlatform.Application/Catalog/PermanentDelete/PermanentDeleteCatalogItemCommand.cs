using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Catalog.PermanentDelete;

public sealed record PermanentDeleteCatalogItemCommand(Guid Id) : ICommand, ITransactionalCommand<LawyerPlatformWritePersistence>, ICacheInvalidator
{
    public IEnumerable<string> Tags => [CatalogCacheTags.All, CatalogCacheTags.Item(Id)];
}
