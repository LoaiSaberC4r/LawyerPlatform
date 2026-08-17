using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class EmailOutboxMessageConfiguration : IWriteEntityConfiguration<EmailOutboxMessage>
{
    public void ConfigureAggregate(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.ToTable("EmailOutboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.NotificationType).HasConversion<int>().IsRequired();
        builder.Property(message => message.IdempotencyKey).HasMaxLength(450).IsRequired();
        builder.Property(message => message.RecipientEmail).HasMaxLength(320).IsRequired();
        builder.Property(message => message.Subject).HasMaxLength(500).IsRequired();
        builder.Property(message => message.HtmlBody).IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(500);
        builder.Property(message => message.Status).HasConversion<int>().IsRequired();
        builder.Property(message => message.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(message => message.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_EmailOutboxMessages_IdempotencyKey");
        builder.HasIndex(message => new { message.Status, message.NextAttemptOnUtc })
            .HasDatabaseName("IX_EmailOutboxMessages_Status_NextAttemptOnUtc");
        builder.HasIndex(message => message.CreatedOnUtc)
            .HasDatabaseName("IX_EmailOutboxMessages_CreatedOnUtc");
    }
}
