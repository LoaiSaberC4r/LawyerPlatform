using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Catalog.Restore;

public sealed record RestoreCatalogItemCommand(Guid Id) : ICommand, ITransactionalCommand<LawyerPlatformWritePersistence>, ICacheInvalidator
{
    public IEnumerable<string> Tags => [CatalogCacheTags.All, CatalogCacheTags.Item(Id)];
}
