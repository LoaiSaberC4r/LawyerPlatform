using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Catalog;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LawyerPlatform.Infrastructure.Persistence;

public sealed class LawyerPlatformDbContext(DbContextOptions<LawyerPlatformDbContext> options)
    : DbContext(options)
{
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<LawyerProfile> LawyerProfiles => Set<LawyerProfile>();
    public DbSet<Governorate> Governorates => Set<Governorate>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<LegalSpecialization> LegalSpecializations => Set<LegalSpecialization>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyWriteConfigurations(typeof(LawyerPlatformDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();

        if (string.Equals(Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
        {
            ConfigureSqliteConcurrencyFallback(modelBuilder);
        }
    }

    private static void ConfigureSqliteConcurrencyFallback(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty("RowVersion");
            if (rowVersion is null)
            {
                continue;
            }

            rowVersion.ValueGenerated = ValueGenerated.Never;
            rowVersion.SetBeforeSaveBehavior(PropertySaveBehavior.Save);
            rowVersion.SetAfterSaveBehavior(PropertySaveBehavior.Save);
        }
    }
}
