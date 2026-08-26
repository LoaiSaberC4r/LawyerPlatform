using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class UserAccountConfiguration : IWriteEntityConfiguration<UserAccount>
{
    public void ConfigureAggregate(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("UserAccounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.UserName).HasMaxLength(50).IsRequired();
        builder.Property(account => account.NormalizedUserName).HasMaxLength(50).IsRequired();
        builder.Property(account => account.Email).HasMaxLength(200).IsRequired();
        builder.Property(account => account.NormalizedEmail).HasMaxLength(200).IsRequired();
        builder.Property(account => account.PhoneNumber).HasMaxLength(30).IsRequired();
        builder.Property(account => account.PasswordHash).IsRequired();
        builder.Property(account => account.Role).HasConversion<int>().IsRequired();
        builder.Property(account => account.Status).HasConversion<int>().IsRequired();
        builder.Property(account => account.IsFirstLogin).IsRequired();
        builder.Property(account => account.PasswordChangedOnUtc).IsRequired();
        builder.Property(account => account.CredentialVersion).HasDefaultValue(1).IsRequired();
        builder.Property(account => account.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(account => account.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("UX_UserAccounts_NormalizedUserName");
        builder.HasIndex(account => account.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("UX_UserAccounts_NormalizedEmail");
        builder.HasIndex(account => account.PhoneNumber)
            .IsUnique()
            .HasDatabaseName("UX_UserAccounts_PhoneNumber");
        builder.HasIndex(account => new { account.Role, account.Status })
            .HasDatabaseName("IX_UserAccounts_Role_Status");
    }
}
