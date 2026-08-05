using System.Text;
using LawyerPlatform.Domain.ReferenceData;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class EgyptLocationSeedCoordinator : IAsyncDisposable
{
    private readonly LawyerPlatformDbContext dbContext;
    private readonly EgyptLocationSeedData catalog;
    private IDbContextTransaction? transaction;
    private HashSet<int>? governorateIds;
    private HashSet<int>? cityIds;
    private SeedStage stage;

    public EgyptLocationSeedCoordinator(LawyerPlatformDbContext dbContext)
        : this(dbContext, EgyptLocationSeedCatalog.Data)
    {
    }

    internal EgyptLocationSeedCoordinator(
        LawyerPlatformDbContext dbContext,
        EgyptLocationSeedData catalog)
    {
        this.dbContext = dbContext;
        this.catalog = catalog;
    }

    public async Task SeedGovernoratesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (stage == SeedStage.Completed)
        {
            ResetForNextExecution();
        }

        EnsureStage(SeedStage.NotStarted, "Governorate seeding must run first.");
        EgyptLocationSeedValidator.Validate(catalog);

        try
        {
            transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var existing = await dbContext.Governorates
                .AsNoTracking()
                .Select(item => new GovernorateSnapshot(item.Id, item.NameAr, item.NameEn))
                .ToListAsync(cancellationToken);

            var existingById = existing.ToDictionary(item => item.Id);
            var existingArabicNames = BuildNameOwners(existing, item => item.NameAr);
            var existingEnglishNames = BuildNameOwners(existing, item => item.NameEn);
            var missing = new List<Governorate>(catalog.Governorates.Count);

            foreach (var seed in catalog.Governorates)
            {
                EnsureNameIsAvailable(existingArabicNames, seed.NameAr, seed.Id, "governorate Arabic");
                EnsureNameIsAvailable(existingEnglishNames, seed.NameEn, seed.Id, "governorate English");

                if (existingById.TryGetValue(seed.Id, out var current))
                {
                    if (!StringComparer.Ordinal.Equals(current.NameAr, seed.NameAr) ||
                        !StringComparer.Ordinal.Equals(current.NameEn, seed.NameEn))
                    {
                        throw new EgyptLocationSeedConflictException(
                            "A Governorate seed ID conflicts with an existing row's approved names.");
                    }

                    continue;
                }

                var result = Governorate.Create(seed.Id, seed.NameAr, seed.NameEn, seed.DisplayOrder);
                if (result.IsFailure)
                {
                    throw new EgyptLocationSeedDataException(
                        "A Governorate seed record failed domain validation.");
                }

                missing.Add(result.Value);
            }

            if (missing.Count > 0)
            {
                await dbContext.Governorates.AddRangeAsync(missing, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            governorateIds = existing.Select(item => item.Id)
                .Concat(catalog.Governorates.Select(seed => seed.Id))
                .ToHashSet();
            stage = SeedStage.GovernoratesSeeded;
        }
        catch (Exception)
        {
            await AbortAsync();
            throw;
        }
    }

    public async Task SeedCitiesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStage(SeedStage.GovernoratesSeeded, "City seeding must run after Governorate seeding.");

        try
        {
            var knownGovernorateIds = governorateIds
                ?? throw new InvalidOperationException("Governorate seed state is unavailable.");
            if (catalog.Cities.Any(seed => !knownGovernorateIds.Contains(seed.GovernorateId)))
            {
                throw new EgyptLocationSeedConflictException(
                    "A City seed record references a Governorate that is missing from the database.");
            }

            var existing = await dbContext.Cities
                .AsNoTracking()
                .Select(item => new CitySnapshot(
                    item.Id,
                    item.GovernorateId,
                    item.NameAr,
                    item.NameEn))
                .ToListAsync(cancellationToken);

            var existingById = existing.ToDictionary(item => item.Id);
            var existingArabicNames = BuildParentNameOwners(
                existing,
                item => item.GovernorateId,
                item => item.NameAr);
            var existingEnglishNames = BuildParentNameOwners(
                existing,
                item => item.GovernorateId,
                item => item.NameEn);
            var missing = new List<City>(catalog.Cities.Count);

            foreach (var seed in catalog.Cities)
            {
                EnsureParentNameIsAvailable(
                    existingArabicNames,
                    seed.GovernorateId,
                    seed.NameAr,
                    seed.Id,
                    "City Arabic");
                EnsureParentNameIsAvailable(
                    existingEnglishNames,
                    seed.GovernorateId,
                    seed.NameEn,
                    seed.Id,
                    "City English");

                if (existingById.TryGetValue(seed.Id, out var current))
                {
                    if (current.GovernorateId != seed.GovernorateId ||
                        !StringComparer.Ordinal.Equals(current.NameAr, seed.NameAr) ||
                        !StringComparer.Ordinal.Equals(current.NameEn, seed.NameEn))
                    {
                        throw new EgyptLocationSeedConflictException(
                            "A City seed ID conflicts with an existing row's parent or approved names.");
                    }

                    continue;
                }

                var result = City.Create(
                    seed.Id,
                    seed.GovernorateId,
                    seed.NameAr,
                    seed.NameEn,
                    seed.DisplayOrder);
                if (result.IsFailure)
                {
                    throw new EgyptLocationSeedDataException("A City seed record failed domain validation.");
                }

                missing.Add(result.Value);
            }

            if (missing.Count > 0)
            {
                await dbContext.Cities.AddRangeAsync(missing, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            cityIds = existing.Select(item => item.Id)
                .Concat(catalog.Cities.Select(seed => seed.Id))
                .ToHashSet();
            stage = SeedStage.CitiesSeeded;
        }
        catch (Exception)
        {
            await AbortAsync();
            throw;
        }
    }

    public async Task SeedAreasAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureStage(SeedStage.CitiesSeeded, "Area seeding must run after City seeding.");

        try
        {
            var knownCityIds = cityIds
                ?? throw new InvalidOperationException("City seed state is unavailable.");
            if (catalog.Areas.Any(seed => !knownCityIds.Contains(seed.CityId)))
            {
                throw new EgyptLocationSeedConflictException(
                    "An Area seed record references a City that is missing from the database.");
            }

            var existing = await dbContext.Areas
                .AsNoTracking()
                .Select(item => new AreaSnapshot(item.Id, item.CityId, item.NameAr, item.NameEn))
                .ToListAsync(cancellationToken);

            var existingById = existing.ToDictionary(item => item.Id);
            var existingArabicNames = BuildParentNameOwners(
                existing,
                item => item.CityId,
                item => item.NameAr);
            var existingEnglishNames = BuildParentNameOwners(
                existing,
                item => item.CityId,
                item => item.NameEn);
            var missing = new List<Area>(catalog.Areas.Count);

            foreach (var seed in catalog.Areas)
            {
                EnsureParentNameIsAvailable(
                    existingArabicNames,
                    seed.CityId,
                    seed.NameAr,
                    seed.Id,
                    "Area Arabic");
                EnsureParentNameIsAvailable(
                    existingEnglishNames,
                    seed.CityId,
                    seed.NameEn,
                    seed.Id,
                    "Area English");

                if (existingById.TryGetValue(seed.Id, out var current))
                {
                    if (current.CityId != seed.CityId ||
                        !StringComparer.Ordinal.Equals(current.NameAr, seed.NameAr) ||
                        !StringComparer.Ordinal.Equals(current.NameEn, seed.NameEn))
                    {
                        throw new EgyptLocationSeedConflictException(
                            "An Area seed ID conflicts with an existing row's parent or approved names.");
                    }

                    continue;
                }

                var result = Area.Create(
                    seed.Id,
                    seed.CityId,
                    seed.NameAr,
                    seed.NameEn,
                    seed.DisplayOrder);
                if (result.IsFailure)
                {
                    throw new EgyptLocationSeedDataException("An Area seed record failed domain validation.");
                }

                missing.Add(result.Value);
            }

            if (missing.Count > 0)
            {
                await dbContext.Areas.AddRangeAsync(missing, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var currentTransaction = transaction
                ?? throw new InvalidOperationException("The Egypt location seed transaction is unavailable.");
            await currentTransaction.CommitAsync(cancellationToken);
            transaction = null;
            await currentTransaction.DisposeAsync();
            stage = SeedStage.Completed;
        }
        catch (Exception)
        {
            await AbortAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await AbortAsync();
        GC.SuppressFinalize(this);
    }

    private static Dictionary<string, int> BuildNameOwners<T>(
        IEnumerable<T> records,
        Func<T, string> nameSelector)
    {
        var owners = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            var id = record switch
            {
                GovernorateSnapshot snapshot => snapshot.Id,
                _ => throw new InvalidOperationException("Unsupported Governorate seed snapshot.")
            };
            AddNameOwner(owners, NormalizeName(nameSelector(record)), id);
        }

        return owners;
    }

    private static Dictionary<EgyptLocationSeedValidator.ParentNameKey, int> BuildParentNameOwners<T>(
        IEnumerable<T> records,
        Func<T, int> parentIdSelector,
        Func<T, string> nameSelector)
        where T : ISeedSnapshot
    {
        var owners = new Dictionary<EgyptLocationSeedValidator.ParentNameKey, int>(
            EgyptLocationSeedValidator.ParentNameKeyComparer.Instance);
        foreach (var record in records)
        {
            var key = new EgyptLocationSeedValidator.ParentNameKey(
                parentIdSelector(record),
                NormalizeName(nameSelector(record)));
            AddNameOwner(owners, key, record.Id);
        }

        return owners;
    }

    private static void AddNameOwner<TKey>(Dictionary<TKey, int> owners, TKey key, int id)
        where TKey : notnull
    {
        if (!owners.TryAdd(key, id) && owners[key] != id)
        {
            owners[key] = 0;
        }
    }

    private static void EnsureNameIsAvailable(
        Dictionary<string, int> owners,
        string name,
        int seedId,
        string field)
    {
        if (owners.TryGetValue(NormalizeName(name), out var ownerId) && ownerId != seedId)
        {
            throw new EgyptLocationSeedConflictException(
                $"A normalized {field} name conflicts with an existing row that has a different ID.");
        }
    }

    private static void EnsureParentNameIsAvailable(
        Dictionary<EgyptLocationSeedValidator.ParentNameKey, int> owners,
        int parentId,
        string name,
        int seedId,
        string field)
    {
        var key = new EgyptLocationSeedValidator.ParentNameKey(parentId, NormalizeName(name));
        if (owners.TryGetValue(key, out var ownerId) && ownerId != seedId)
        {
            throw new EgyptLocationSeedConflictException(
                $"A normalized {field} name conflicts under the same parent with a different ID.");
        }
    }

    private static string NormalizeName(string name)
        => name.Trim().Normalize(NormalizationForm.FormC);

    private void EnsureStage(SeedStage requiredStage, string message)
    {
        if (stage != requiredStage)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void ResetForNextExecution()
    {
        governorateIds = null;
        cityIds = null;
        stage = SeedStage.NotStarted;
    }

    private async Task AbortAsync()
    {
        if (transaction is null)
        {
            return;
        }

        var currentTransaction = transaction;
        transaction = null;
        stage = SeedStage.Failed;
        try
        {
            await currentTransaction.RollbackAsync(CancellationToken.None);
        }
        finally
        {
            await currentTransaction.DisposeAsync();
        }
    }

    private interface ISeedSnapshot
    {
        int Id { get; }
    }

    private sealed record GovernorateSnapshot(int Id, string NameAr, string NameEn) : ISeedSnapshot;

    private sealed record CitySnapshot(
        int Id,
        int GovernorateId,
        string NameAr,
        string NameEn) : ISeedSnapshot;

    private sealed record AreaSnapshot(int Id, int CityId, string NameAr, string NameEn) : ISeedSnapshot;

    private enum SeedStage
    {
        NotStarted,
        GovernoratesSeeded,
        CitiesSeeded,
        Completed,
        Failed
    }
}
