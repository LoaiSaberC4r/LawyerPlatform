using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.SharedDto;

namespace LawyerPlatform.Application.Catalog.GetPage;

public sealed record GetCatalogItemsPageQuery : ICacheableQuery<PagedResult<CatalogItemResponse>>
{
    public string? SearchText { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public TimeSpan? TimeToLive => TimeSpan.FromMinutes(2);

    public IEnumerable<string> Tags => [CatalogCacheTags.All];
}
