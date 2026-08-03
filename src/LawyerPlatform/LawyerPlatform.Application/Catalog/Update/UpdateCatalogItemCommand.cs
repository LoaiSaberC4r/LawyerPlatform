using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Catalog.Update;

public sealed record UpdateCatalogItemCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal Price) : ICommand<CatalogItemResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>, ICacheInvalidator
{
    public IEnumerable<string> Tags => [CatalogCacheTags.All, CatalogCacheTags.Item(Id)];
}
