namespace LawyerPlatform.Application.Catalog;

internal static class CatalogCacheTags
{
    public const string All = "catalog-items";

    public static string Item(Guid id) => $"catalog-item:{id:N}";
}
