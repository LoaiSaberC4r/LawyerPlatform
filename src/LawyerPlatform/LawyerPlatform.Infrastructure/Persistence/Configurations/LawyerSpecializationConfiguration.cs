using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class LawyerSpecializationConfiguration : IWriteEntityConfiguration<LawyerSpecialization>
{
    public void ConfigureAggregate(EntityTypeBuilder<LawyerSpecialization> builder)
    {
        builder.ToTable("LawyerSpecializations");
        builder.HasKey(item => new { item.LawyerProfileId, item.LegalSpecializationId });
        builder.HasOne(item => item.LawyerProfile).WithMany(profile => profile.Specializations)
            .HasForeignKey(item => item.LawyerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.LegalSpecialization).WithMany()
            .HasForeignKey(item => item.LegalSpecializationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.LegalSpecializationId, item.LawyerProfileId })
            .HasDatabaseName("IX_LawyerSpecializations_LegalSpecializationId_LawyerProfileId");
    }
}
