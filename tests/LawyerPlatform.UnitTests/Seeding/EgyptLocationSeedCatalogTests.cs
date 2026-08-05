using LawyerPlatform.Infrastructure.Seeding;

namespace LawyerPlatform.UnitTests.Seeding;

public sealed class EgyptLocationSeedCatalogTests
{
    [Fact]
    public void CatalogContainsTheApprovedRecordCounts()
    {
        Assert.Equal(
            EgyptLocationSeedCatalog.ExpectedGovernorateCount,
            EgyptLocationSeedCatalog.Governorates.Count);
        Assert.Equal(
            EgyptLocationSeedCatalog.ExpectedCityCount,
            EgyptLocationSeedCatalog.Cities.Count);
        Assert.Equal(
            EgyptLocationSeedCatalog.ExpectedAreaCount,
            EgyptLocationSeedCatalog.Areas.Count);
    }

    [Fact]
    public void EveryLevelHasUniquePositiveStableIds()
    {
        AssertUniquePositiveIds(EgyptLocationSeedCatalog.Governorates.Select(seed => seed.Id));
        AssertUniquePositiveIds(EgyptLocationSeedCatalog.Cities.Select(seed => seed.Id));
        AssertUniquePositiveIds(EgyptLocationSeedCatalog.Areas.Select(seed => seed.Id));

        Assert.All(
            EgyptLocationSeedCatalog.Governorates,
            seed => Assert.Equal(seed.DisplayOrder, seed.Id));
        Assert.All(
            EgyptLocationSeedCatalog.Cities,
            seed => Assert.Equal(seed.GovernorateId * 1000 + seed.DisplayOrder, seed.Id));
        Assert.All(
            EgyptLocationSeedCatalog.Areas,
            seed =>
            {
                var expectedId = (long)seed.CityId * 10000 + seed.DisplayOrder;
                Assert.InRange(expectedId, 1, int.MaxValue);
                Assert.Equal(expectedId, seed.Id);
            });
    }

    [Fact]
    public void EveryRecordHasTrimmedArabicAndEnglishNames()
    {
        AssertNames(EgyptLocationSeedCatalog.Governorates.Select(seed => (seed.NameAr, seed.NameEn)));
        AssertNames(EgyptLocationSeedCatalog.Cities.Select(seed => (seed.NameAr, seed.NameEn)));
        AssertNames(EgyptLocationSeedCatalog.Areas.Select(seed => (seed.NameAr, seed.NameEn)));
    }

    [Fact]
    public void HierarchyReferencesExistingParentsAndEveryParentHasChildren()
    {
        var governorateIds = EgyptLocationSeedCatalog.Governorates
            .Select(seed => seed.Id)
            .ToHashSet();
        var cityIds = EgyptLocationSeedCatalog.Cities
            .Select(seed => seed.Id)
            .ToHashSet();

        Assert.All(
            EgyptLocationSeedCatalog.Cities,
            seed => Assert.Contains(seed.GovernorateId, governorateIds));
        Assert.All(
            EgyptLocationSeedCatalog.Areas,
            seed => Assert.Contains(seed.CityId, cityIds));
        Assert.Equal(
            governorateIds.Order(),
            EgyptLocationSeedCatalog.Cities.Select(seed => seed.GovernorateId).Distinct().Order());
        Assert.Equal(
            cityIds.Order(),
            EgyptLocationSeedCatalog.Areas.Select(seed => seed.CityId).Distinct().Order());
    }

    [Fact]
    public void DisplayOrdersAreUniqueAndContiguousWithinEveryParent()
    {
        AssertContiguous(EgyptLocationSeedCatalog.Governorates.Select(seed => seed.DisplayOrder));
        Assert.All(
            EgyptLocationSeedCatalog.Cities.GroupBy(seed => seed.GovernorateId),
            group => AssertContiguous(group.Select(seed => seed.DisplayOrder)));
        Assert.All(
            EgyptLocationSeedCatalog.Areas.GroupBy(seed => seed.CityId),
            group => AssertContiguous(group.Select(seed => seed.DisplayOrder)));
    }

    [Fact]
    public void NamesAreUniqueAtTheirApprovedScope()
    {
        Assert.Equal(
            EgyptLocationSeedCatalog.Governorates.Count,
            EgyptLocationSeedCatalog.Governorates
                .Select(seed => seed.NameAr)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.Equal(
            EgyptLocationSeedCatalog.Governorates.Count,
            EgyptLocationSeedCatalog.Governorates
                .Select(seed => seed.NameEn)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        AssertScopedNamesAreUnique(
            EgyptLocationSeedCatalog.Cities,
            seed => seed.GovernorateId,
            seed => seed.NameAr,
            seed => seed.NameEn);
        AssertScopedNamesAreUnique(
            EgyptLocationSeedCatalog.Areas,
            seed => seed.CityId,
            seed => seed.NameAr,
            seed => seed.NameEn);
    }

    [Fact]
    public void LoaderReturnsTheSameImmutableCatalogInstances()
    {
        var first = EgyptLocationSeedDataLoader.Load();
        var second = EgyptLocationSeedDataLoader.Load();

        Assert.Same(first, second);
        Assert.Same(first.Governorates, EgyptLocationSeedCatalog.Governorates);
        Assert.Same(first.Cities, EgyptLocationSeedCatalog.Cities);
        Assert.Same(first.Areas, EgyptLocationSeedCatalog.Areas);
        Assert.False(first.Governorates is List<GovernorateSeed>);
        Assert.False(first.Cities is List<CitySeed>);
        Assert.False(first.Areas is List<AreaSeed>);
    }

    [Fact]
    public void ValidatorRejectsMissingGovernorateBeforeSeeding()
    {
        var invalidCities = EgyptLocationSeedCatalog.Cities.ToArray();
        invalidCities[0] = invalidCities[0] with { GovernorateId = int.MaxValue };
        var invalid = new EgyptLocationSeedData(
            EgyptLocationSeedCatalog.Governorates,
            invalidCities,
            EgyptLocationSeedCatalog.Areas);

        var exception = Assert.Throws<EgyptLocationSeedDataException>(
            () => EgyptLocationSeedValidator.Validate(invalid));

        Assert.Equal("The Egypt city catalog references a missing governorate.", exception.Message);
    }

    [Fact]
    public void ValidatorRejectsMissingCityBeforeSeeding()
    {
        var invalidAreas = EgyptLocationSeedCatalog.Areas.ToArray();
        invalidAreas[0] = invalidAreas[0] with { CityId = int.MaxValue };
        var invalid = new EgyptLocationSeedData(
            EgyptLocationSeedCatalog.Governorates,
            EgyptLocationSeedCatalog.Cities,
            invalidAreas);

        var exception = Assert.Throws<EgyptLocationSeedDataException>(
            () => EgyptLocationSeedValidator.Validate(invalid));

        Assert.Equal("The Egypt area catalog references a missing city.", exception.Message);
    }

    private static void AssertUniquePositiveIds(IEnumerable<int> ids)
    {
        var values = ids.ToArray();
        Assert.All(values, id => Assert.True(id > 0));
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    private static void AssertNames(IEnumerable<(string NameAr, string NameEn)> names)
    {
        Assert.All(
            names,
            names =>
            {
                Assert.False(string.IsNullOrWhiteSpace(names.NameAr));
                Assert.False(string.IsNullOrWhiteSpace(names.NameEn));
                Assert.Equal(names.NameAr.Trim(), names.NameAr);
                Assert.Equal(names.NameEn.Trim(), names.NameEn);
            });
    }

    private static void AssertContiguous(IEnumerable<int> displayOrders)
    {
        var actual = displayOrders.Order().ToArray();
        Assert.Equal(Enumerable.Range(1, actual.Length), actual);
        Assert.Equal(actual.Length, actual.Distinct().Count());
    }

    private static void AssertScopedNamesAreUnique<T>(
        IEnumerable<T> records,
        Func<T, int> parentSelector,
        Func<T, string> arabicNameSelector,
        Func<T, string> englishNameSelector)
    {
        foreach (var group in records.GroupBy(parentSelector))
        {
            Assert.Equal(
                group.Count(),
                group.Select(arabicNameSelector).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(
                group.Count(),
                group.Select(englishNameSelector).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
