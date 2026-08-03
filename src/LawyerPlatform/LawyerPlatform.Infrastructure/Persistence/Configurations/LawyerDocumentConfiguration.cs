using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class LawyerDocumentConfiguration : IWriteEntityConfiguration<LawyerDocument>
{
    public void ConfigureAggregate(EntityTypeBuilder<LawyerDocument> builder)
    {
        builder.ToTable("LawyerDocuments");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.DocumentType).HasMaxLength(100).IsRequired();
        builder.Property(document => document.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(document => document.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(document => document.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(document => document.LawyerProfile).WithMany(profile => profile.Documents)
            .HasForeignKey(document => document.LawyerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(document => new { document.LawyerProfileId, document.DocumentType, document.IsDeleted })
            .HasDatabaseName("IX_LawyerDocuments_LawyerProfileId_DocumentType_IsDeleted");
    }
}
