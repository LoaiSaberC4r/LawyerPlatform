using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class LawyerAvailabilityConfiguration : IWriteEntityConfiguration<LawyerAvailability>
{
    public void ConfigureAggregate(EntityTypeBuilder<LawyerAvailability> builder)
    {
        builder.ToTable("LawyerAvailabilities");
        builder.HasKey(availability => availability.Id);
        builder.Property(availability => availability.DayOfWeek)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(availability => availability.StartTime)
            .HasColumnType("time")
            .IsRequired();
        builder.Property(availability => availability.EndTime)
            .HasColumnType("time")
            .IsRequired();
        builder.HasOne(availability => availability.LawyerConsultationSettings)
            .WithMany(settings => settings.Availability)
            .HasForeignKey(availability => availability.LawyerConsultationSettingsId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(availability => new
            {
                availability.LawyerConsultationSettingsId,
                availability.DayOfWeek
            })
            .IsUnique()
            .HasDatabaseName("UX_LawyerAvailabilities_SettingsId_DayOfWeek");
    }
}
