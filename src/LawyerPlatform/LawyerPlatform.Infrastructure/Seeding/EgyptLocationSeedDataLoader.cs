using System.Reflection;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LawyerPlatform.Infrastructure.Seeding;

internal static class EgyptLocationSeedDataLoader
{
    private const string GovernoratesResource =
        "LawyerPlatform.Infrastructure.Seeding.Data.egypt-governorates.v1.json";
    private const string CitiesResource =
        "LawyerPlatform.Infrastructure.Seeding.Data.egypt-cities.v1.json";
    private const string AreasResource =
        "LawyerPlatform.Infrastructure.Seeding.Data.egypt-areas.v1.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static readonly Lazy<EgyptLocationSeedData> CachedData = new(
        LoadCore,
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static EgyptLocationSeedData Load() => CachedData.Value;

    private static EgyptLocationSeedData LoadCore()
    {
        var assembly = typeof(EgyptLocationSeedDataLoader).Assembly;
        return new EgyptLocationSeedData(
            LoadResource<GovernorateSeed>(assembly, GovernoratesResource, "governorates"),
            LoadResource<CitySeed>(assembly, CitiesResource, "cities"),
            LoadResource<AreaSeed>(assembly, AreasResource, "areas"));
    }

    private static ReadOnlyCollection<T> LoadResource<T>(
        Assembly assembly,
        string resourceName,
        string catalogName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new EgyptLocationSeedDataException(
                $"The embedded Egypt {catalogName} seed resource is missing.");
        }

        try
        {
            var records = JsonSerializer.Deserialize<T[]>(stream, SerializerOptions);
            if (records is null)
            {
                throw new EgyptLocationSeedDataException(
                    $"The embedded Egypt {catalogName} seed resource contains a null collection.");
            }

            return Array.AsReadOnly(records);
        }
        catch (JsonException exception)
        {
            throw new EgyptLocationSeedDataException(
                $"The embedded Egypt {catalogName} seed resource contains malformed or unsupported JSON.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new EgyptLocationSeedDataException(
                $"The embedded Egypt {catalogName} seed resource contains malformed or unsupported JSON.",
                exception);
        }
    }
}
