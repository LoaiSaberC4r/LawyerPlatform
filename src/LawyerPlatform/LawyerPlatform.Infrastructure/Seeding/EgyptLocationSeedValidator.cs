using System.Text;

namespace LawyerPlatform.Infrastructure.Seeding;

internal static class EgyptLocationSeedValidator
{
    private const int MaximumNameLength = 150;

    public static void Validate(EgyptLocationSeedData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        ValidateGovernorates(data.Governorates);
        ValidateCities(data.Cities, data.Governorates);
        ValidateAreas(data.Areas, data.Cities);
        ValidateHierarchyCoverage(data);
    }

    private static void ValidateGovernorates(IReadOnlyList<GovernorateSeed> governorates)
    {
        if (governorates.Count != EgyptLocationSeedCatalog.ExpectedGovernorateCount)
        {
            throw new EgyptLocationSeedDataException(
                $"The Egypt governorate catalog must contain exactly {EgyptLocationSeedCatalog.ExpectedGovernorateCount} records.");
        }

        var ids = new HashSet<int>();
        var arabicNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var englishNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var displayOrders = new HashSet<int>();

        foreach (var seed in governorates)
        {
            ValidateCommon(seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder, "governorate");
            AddUnique(ids, seed.Id, "The Egypt governorate catalog contains a duplicate ID.");
            AddUnique(arabicNames, NormalizeName(seed.NameAr), "The Egypt governorate catalog contains a duplicate Arabic name.");
            AddUnique(englishNames, NormalizeName(seed.NameEn), "The Egypt governorate catalog contains a duplicate English name.");
            AddUnique(displayOrders, seed.DisplayOrder, "The Egypt governorate catalog contains a duplicate display order.");

            if (seed.Id != seed.DisplayOrder)
            {
                throw new EgyptLocationSeedDataException(
                    "Egypt governorate IDs and display orders must be the stable sequence from 1 through 27.");
            }
        }

        ValidateContiguousOrders(displayOrders, governorates.Count, "governorate");
    }

    private static void ValidateCities(
        IReadOnlyList<CitySeed> cities,
        IReadOnlyList<GovernorateSeed> governorates)
    {
        if (cities.Count == 0)
        {
            throw new EgyptLocationSeedDataException("The Egypt city catalog must not be empty.");
        }

        var governorateIds = governorates.Select(seed => seed.Id).ToHashSet();
        var ids = new HashSet<int>();
        var arabicNames = new HashSet<ParentNameKey>(ParentNameKeyComparer.Instance);
        var englishNames = new HashSet<ParentNameKey>(ParentNameKeyComparer.Instance);
        var displayOrders = new HashSet<ParentOrderKey>();

        foreach (var seed in cities)
        {
            ValidateCommon(seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder, "city");
            if (seed.GovernorateId <= 0 || !governorateIds.Contains(seed.GovernorateId))
            {
                throw new EgyptLocationSeedDataException(
                    "The Egypt city catalog references a missing governorate.");
            }

            AddUnique(ids, seed.Id, "The Egypt city catalog contains a duplicate ID.");
            AddUnique(
                arabicNames,
                new ParentNameKey(seed.GovernorateId, NormalizeName(seed.NameAr)),
                "The Egypt city catalog contains a duplicate Arabic name under one governorate.");
            AddUnique(
                englishNames,
                new ParentNameKey(seed.GovernorateId, NormalizeName(seed.NameEn)),
                "The Egypt city catalog contains a duplicate English name under one governorate.");
            AddUnique(
                displayOrders,
                new ParentOrderKey(seed.GovernorateId, seed.DisplayOrder),
                "The Egypt city catalog contains a duplicate display order under one governorate.");

            if (seed.Id != seed.GovernorateId * 1000 + seed.DisplayOrder)
            {
                throw new EgyptLocationSeedDataException(
                    "An Egypt city ID does not match the stable parent-and-local-sequence strategy.");
            }
        }

        foreach (var group in cities.GroupBy(seed => seed.GovernorateId))
        {
            ValidateContiguousOrders(
                group.Select(seed => seed.DisplayOrder),
                group.Count(),
                "city");
        }
    }

    private static void ValidateAreas(
        IReadOnlyList<AreaSeed> areas,
        IReadOnlyList<CitySeed> cities)
    {
        if (areas.Count == 0)
        {
            throw new EgyptLocationSeedDataException("The Egypt area catalog must not be empty.");
        }

        var cityIds = cities.Select(seed => seed.Id).ToHashSet();
        var ids = new HashSet<int>();
        var arabicNames = new HashSet<ParentNameKey>(ParentNameKeyComparer.Instance);
        var englishNames = new HashSet<ParentNameKey>(ParentNameKeyComparer.Instance);
        var displayOrders = new HashSet<ParentOrderKey>();

        foreach (var seed in areas)
        {
            ValidateCommon(seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder, "area");
            if (seed.CityId <= 0 || !cityIds.Contains(seed.CityId))
            {
                throw new EgyptLocationSeedDataException(
                    "The Egypt area catalog references a missing city.");
            }

            AddUnique(ids, seed.Id, "The Egypt area catalog contains a duplicate ID.");
            AddUnique(
                arabicNames,
                new ParentNameKey(seed.CityId, NormalizeName(seed.NameAr)),
                "The Egypt area catalog contains a duplicate Arabic name under one city.");
            AddUnique(
                englishNames,
                new ParentNameKey(seed.CityId, NormalizeName(seed.NameEn)),
                "The Egypt area catalog contains a duplicate English name under one city.");
            AddUnique(
                displayOrders,
                new ParentOrderKey(seed.CityId, seed.DisplayOrder),
                "The Egypt area catalog contains a duplicate display order under one city.");

            var expectedId = (long)seed.CityId * 10000 + seed.DisplayOrder;
            if (expectedId > int.MaxValue || seed.Id != expectedId)
            {
                throw new EgyptLocationSeedDataException(
                    "An Egypt area ID is outside Int32 or does not match the stable parent-and-local-sequence strategy.");
            }
        }

        foreach (var group in areas.GroupBy(seed => seed.CityId))
        {
            ValidateContiguousOrders(
                group.Select(seed => seed.DisplayOrder),
                group.Count(),
                "area");
        }
    }

    private static void ValidateHierarchyCoverage(EgyptLocationSeedData data)
    {
        var governoratesWithCities = data.Cities.Select(seed => seed.GovernorateId).ToHashSet();
        if (data.Governorates.Any(seed => !governoratesWithCities.Contains(seed.Id)))
        {
            throw new EgyptLocationSeedDataException(
                "Every Egypt governorate seed record must contain at least one city.");
        }

        var citiesWithAreas = data.Areas.Select(seed => seed.CityId).ToHashSet();
        if (data.Cities.Any(seed => !citiesWithAreas.Contains(seed.Id)))
        {
            throw new EgyptLocationSeedDataException(
                "Every Egypt city seed record must contain at least one area.");
        }
    }

    private static void ValidateCommon(
        int id,
        string? nameAr,
        string? nameEn,
        int displayOrder,
        string level)
    {
        if (id <= 0)
        {
            throw new EgyptLocationSeedDataException($"An Egypt {level} seed ID is not positive.");
        }

        if (displayOrder <= 0)
        {
            throw new EgyptLocationSeedDataException(
                $"An Egypt {level} seed display order is not positive.");
        }

        ValidateName(nameAr, level, "Arabic");
        ValidateName(nameEn, level, "English");
    }

    private static void ValidateName(string? name, string level, string language)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new EgyptLocationSeedDataException(
                $"An Egypt {level} seed has an empty {language} name.");
        }

        if (!string.Equals(name, name.Trim(), StringComparison.Ordinal))
        {
            throw new EgyptLocationSeedDataException(
                $"An Egypt {level} seed has surrounding whitespace in its {language} name.");
        }

        if (name.Length > MaximumNameLength)
        {
            throw new EgyptLocationSeedDataException(
                $"An Egypt {level} seed {language} name exceeds the database length limit.");
        }
    }

    private static string NormalizeName(string name)
        => name.Trim().Normalize(NormalizationForm.FormC);

    private static void ValidateContiguousOrders(
        IEnumerable<int> displayOrders,
        int expectedCount,
        string level)
    {
        var expected = 1;
        foreach (var displayOrder in displayOrders.Order())
        {
            if (displayOrder != expected)
            {
                throw new EgyptLocationSeedDataException(
                    $"Egypt {level} display orders must start at 1 and remain contiguous within each parent.");
            }

            expected++;
        }

        if (expected - 1 != expectedCount)
        {
            throw new EgyptLocationSeedDataException(
                $"Egypt {level} display orders do not match the catalog record count.");
        }
    }

    private static void AddUnique<T>(HashSet<T> values, T value, string message)
    {
        if (!values.Add(value))
        {
            throw new EgyptLocationSeedDataException(message);
        }
    }

    private readonly record struct ParentOrderKey(int ParentId, int DisplayOrder);

    internal readonly record struct ParentNameKey(int ParentId, string Name);

    internal sealed class ParentNameKeyComparer : IEqualityComparer<ParentNameKey>
    {
        public static ParentNameKeyComparer Instance { get; } = new();

        public bool Equals(ParentNameKey x, ParentNameKey y)
            => x.ParentId == y.ParentId &&
               StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);

        public int GetHashCode(ParentNameKey obj)
            => HashCode.Combine(
                obj.ParentId,
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}
