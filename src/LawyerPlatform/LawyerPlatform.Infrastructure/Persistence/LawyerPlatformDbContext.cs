using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Infrastructure.Persistence;

public sealed class LawyerPlatformDbContext(DbContextOptions<LawyerPlatformDbContext> options)
    : DbContext(options)
{
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyWriteConfigurations(typeof(LawyerPlatformDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();
    }
}
