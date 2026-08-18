using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class LawyerOfficeConfiguration : IWriteEntityConfiguration<LawyerOffice>
{
    public void ConfigureAggregate(EntityTypeBuilder<LawyerOffice> builder)
    {
        builder.ToTable("LawyerOffices");
        builder.HasKey(office => office.Id);
        builder.Property(office => office.DetailedAddress).HasMaxLength(500).IsRequired();
        builder.Property(office => office.PublicPhoneNumber).HasMaxLength(30);
        builder.Property(office => office.Latitude).HasPrecision(9, 6);
        builder.Property(office => office.Longitude).HasPrecision(9, 6);
        builder.Property(office => office.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(office => office.LawyerProfile).WithMany(profile => profile.Offices)
            .HasForeignKey(office => office.LawyerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(office => office.Governorate).WithMany()
            .HasForeignKey(office => office.GovernorateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(office => office.City).WithMany()
            .HasForeignKey(office => office.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(office => office.Area).WithMany()
            .HasForeignKey(office => office.AreaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(office => office.LawyerProfileId).IsUnique()
            .HasFilter("[IsPrimary] = 1").HasDatabaseName("UX_LawyerOffices_LawyerProfileId_Primary");
        builder.HasIndex(office => new { office.GovernorateId, office.CityId, office.AreaId })
            .HasDatabaseName("IX_LawyerOffices_GovernorateId_CityId_AreaId");
    }
}
