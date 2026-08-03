using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class GovernorateConfiguration : IWriteEntityConfiguration<Governorate>
{
    public void ConfigureAggregate(EntityTypeBuilder<Governorate> builder)
    {
        builder.ToTable("Governorates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.DisplayOrder).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.IsActive, item.DisplayOrder })
            .HasDatabaseName("IX_Governorates_IsActive_DisplayOrder");
    }
}
