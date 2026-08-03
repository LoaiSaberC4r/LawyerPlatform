using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using LawyerPlatform.Domain.Catalog;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class CatalogItemConfiguration : IWriteEntityConfiguration<CatalogItem>
{
    public void ConfigureAggregate(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable("CatalogItems");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasMaxLength(2000);

        builder.Property(item => item.Price)
            .HasPrecision(18, 2);

        builder.Property(item => item.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(item => item.Name);
    }
}
