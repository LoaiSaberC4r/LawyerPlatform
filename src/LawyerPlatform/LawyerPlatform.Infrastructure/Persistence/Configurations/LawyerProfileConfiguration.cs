using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class LawyerProfileConfiguration : IWriteEntityConfiguration<LawyerProfile>
{
    public void ConfigureAggregate(EntityTypeBuilder<LawyerProfile> builder)
    {
        builder.ToTable("LawyerProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.FullName).HasMaxLength(200).IsRequired();
        builder.Property(profile => profile.ProfessionalTitle).HasMaxLength(200);
        builder.Property(profile => profile.Biography).HasMaxLength(4000);
        builder.Property(profile => profile.ProfessionalRegistrationNumber).HasMaxLength(100);
        builder.Property(profile => profile.ProfileImageStorageKey).HasMaxLength(500);
        builder.Property(profile => profile.ApprovalStatus).HasConversion<int>().IsRequired();
        builder.Property(profile => profile.ApprovalReason).HasMaxLength(1000);
        builder.Property(profile => profile.SuspensionReason).HasMaxLength(1000);
        builder.Property(profile => profile.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(profile => profile.UserAccount)
            .WithOne()
            .HasForeignKey<LawyerProfile>(profile => profile.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(profile => profile.UserAccountId)
            .IsUnique()
            .HasDatabaseName("UX_LawyerProfiles_UserAccountId");
        builder.HasIndex(profile => profile.ProfessionalRegistrationNumber)
            .IsUnique()
            .HasFilter("[ProfessionalRegistrationNumber] IS NOT NULL")
            .HasDatabaseName("UX_LawyerProfiles_ProfessionalRegistrationNumber");
        builder.HasIndex(profile => new { profile.ApprovalStatus, profile.SubmittedOnUtc })
            .HasDatabaseName("IX_LawyerProfiles_ApprovalStatus_SubmittedOnUtc");
        builder.Navigation(profile => profile.Offices).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.Specializations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(profile => profile.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
