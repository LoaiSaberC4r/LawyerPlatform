using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ClientProfileConfiguration : IWriteEntityConfiguration<ClientProfile>
{
    public void ConfigureAggregate(EntityTypeBuilder<ClientProfile> builder)
    {
        builder.ToTable("ClientProfiles");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.FullName).HasMaxLength(200).IsRequired();
        builder.Property(profile => profile.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(profile => profile.UserAccount)
            .WithOne()
            .HasForeignKey<ClientProfile>(profile => profile.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(profile => profile.UserAccountId)
            .IsUnique()
            .HasDatabaseName("UX_ClientProfiles_UserAccountId");
    }
}
