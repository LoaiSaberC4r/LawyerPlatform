using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class CityConfiguration : IWriteEntityConfiguration<City>
{
    public void ConfigureAggregate(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.DisplayOrder).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.Governorate)
            .WithMany()
            .HasForeignKey(item => item.GovernorateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.GovernorateId).HasDatabaseName("IX_Cities_GovernorateId");
        builder.HasIndex(item => new { item.GovernorateId, item.IsActive, item.DisplayOrder })
            .HasDatabaseName("IX_Cities_GovernorateId_IsActive_DisplayOrder");
    }
}
