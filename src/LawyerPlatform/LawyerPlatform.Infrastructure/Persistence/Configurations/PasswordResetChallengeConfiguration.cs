using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetChallengeConfiguration
    : IWriteEntityConfiguration<PasswordResetChallenge>
{
    public void ConfigureAggregate(EntityTypeBuilder<PasswordResetChallenge> builder)
    {
        builder.ToTable("PasswordResetChallenges");
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.UserAccountId).IsRequired();
        builder.Property(challenge => challenge.OtpHash).HasMaxLength(128).IsRequired();
        builder.Property(challenge => challenge.CreatedOnUtc).IsRequired();
        builder.Property(challenge => challenge.ExpiresOnUtc).IsRequired();
        builder.Property(challenge => challenge.FailedAttemptCount).IsRequired();
        builder.Property(challenge => challenge.ResetTokenHash).HasMaxLength(128);
        builder.Property(challenge => challenge.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(challenge => challenge.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(challenge => new { challenge.UserAccountId, challenge.CreatedOnUtc })
            .HasDatabaseName("IX_PasswordResetChallenges_UserAccountId_CreatedOnUtc");
        builder.HasIndex(challenge => challenge.ExpiresOnUtc)
            .HasDatabaseName("IX_PasswordResetChallenges_ExpiresOnUtc");
        builder.HasIndex(challenge => challenge.UserAccountId)
            .IsUnique()
            .HasFilter("[InvalidatedOnUtc] IS NULL AND [ConsumedOnUtc] IS NULL")
            .HasDatabaseName("UX_PasswordResetChallenges_Current_UserAccountId");
    }
}
