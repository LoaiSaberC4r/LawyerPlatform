using BuildingBlock.Application.Abstraction;

namespace LawyerPlatform.Application.Catalog.GetById;

public sealed record GetCatalogItemByIdQuery(Guid Id) : ICacheableQuery<CatalogItemResponse>
{
    public string? CacheKey => $"catalog-item:{Id:N}";
    public TimeSpan? TimeToLive => TimeSpan.FromMinutes(5);
    public IEnumerable<string> Tags => [CatalogCacheTags.Item(Id)];
}
