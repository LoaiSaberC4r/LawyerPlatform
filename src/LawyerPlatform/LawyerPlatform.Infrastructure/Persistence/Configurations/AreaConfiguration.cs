using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class AreaConfiguration : IWriteEntityConfiguration<Area>
{
    public void ConfigureAggregate(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.DisplayOrder).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.City)
            .WithMany()
            .HasForeignKey(item => item.CityId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.CityId).HasDatabaseName("IX_Areas_CityId");
        builder.HasIndex(item => new { item.CityId, item.IsActive, item.DisplayOrder })
            .HasDatabaseName("IX_Areas_CityId_IsActive_DisplayOrder");
    }
}
