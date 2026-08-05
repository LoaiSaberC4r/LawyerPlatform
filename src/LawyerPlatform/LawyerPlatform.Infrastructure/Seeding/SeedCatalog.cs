namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed record GovernorateSeed(int Id, string NameAr, string NameEn, int DisplayOrder);
internal sealed record CitySeed(int Id, int GovernorateId, string NameAr, string NameEn, int DisplayOrder);
internal sealed record AreaSeed(int Id, int CityId, string NameAr, string NameEn, int DisplayOrder);
internal sealed record LegalSpecializationSeed(int Id, string NameAr, string NameEn, int DisplayOrder);

internal static class SeedCatalog
{
    public static IReadOnlyList<GovernorateSeed> Governorates => EgyptLocationSeedCatalog.Governorates;
    public static IReadOnlyList<CitySeed> Cities => EgyptLocationSeedCatalog.Cities;
    public static IReadOnlyList<AreaSeed> Areas => EgyptLocationSeedCatalog.Areas;
    public static IReadOnlyList<LegalSpecializationSeed> LegalSpecializations { get; } = [];
}
