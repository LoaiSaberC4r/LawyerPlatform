namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed record GovernorateSeed(int Id, string NameAr, string NameEn, int DisplayOrder);
internal sealed record CitySeed(int Id, int GovernorateId, string NameAr, string NameEn, int DisplayOrder);
internal sealed record AreaSeed(int Id, int CityId, string NameAr, string NameEn, int DisplayOrder);
internal sealed record LegalSpecializationSeed(int Id, string NameAr, string NameEn, int DisplayOrder);

internal static class SeedCatalog
{
    // No approved values were supplied. Populate only from an owner-approved, stable catalog.
    public static IReadOnlyList<GovernorateSeed> Governorates { get; } = [];
    public static IReadOnlyList<CitySeed> Cities { get; } = [];
    public static IReadOnlyList<AreaSeed> Areas { get; } = [];
    public static IReadOnlyList<LegalSpecializationSeed> LegalSpecializations { get; } = [];
}
