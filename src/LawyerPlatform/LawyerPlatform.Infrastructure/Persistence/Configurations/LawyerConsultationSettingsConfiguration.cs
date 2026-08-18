using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class LawyerConsultationSettingsConfiguration
    : IWriteEntityConfiguration<LawyerConsultationSettings>
{
    public void ConfigureAggregate(EntityTypeBuilder<LawyerConsultationSettings> builder)
    {
        builder.ToTable("LawyerConsultationSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.OnlineConsultationPrice)
            .HasColumnName("ConsultationPrice")
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(settings => settings.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
        builder.HasOne(settings => settings.LawyerProfile)
            .WithOne(profile => profile.ConsultationSettings)
            .HasForeignKey<LawyerConsultationSettings>(settings => settings.LawyerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(settings => settings.LawyerProfileId)
            .IsUnique()
            .HasDatabaseName("UX_LawyerConsultationSettings_LawyerProfileId");
        builder.Navigation(settings => settings.Availability)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
