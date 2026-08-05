namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed record EgyptLocationSeedData(
    IReadOnlyList<GovernorateSeed> Governorates,
    IReadOnlyList<CitySeed> Cities,
    IReadOnlyList<AreaSeed> Areas);

internal static class EgyptLocationSeedCatalog
{
    public const int ExpectedGovernorateCount = 27;
    public const int ExpectedCityCount = 351;
    public const int ExpectedAreaCount = 5716;

    private static readonly Lazy<EgyptLocationSeedData> ValidatedData = new(
        LoadAndValidate,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyList<GovernorateSeed> Governorates => Data.Governorates;
    public static IReadOnlyList<CitySeed> Cities => Data.Cities;
    public static IReadOnlyList<AreaSeed> Areas => Data.Areas;
    public static EgyptLocationSeedData Data => ValidatedData.Value;

    private static EgyptLocationSeedData LoadAndValidate()
    {
        var data = EgyptLocationSeedDataLoader.Load();
        EgyptLocationSeedValidator.Validate(data);
        return data;
    }
}
