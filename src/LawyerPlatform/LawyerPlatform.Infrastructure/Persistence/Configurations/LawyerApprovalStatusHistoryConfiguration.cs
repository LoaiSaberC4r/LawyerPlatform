using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class LawyerApprovalStatusHistoryConfiguration : IWriteEntityConfiguration<LawyerApprovalStatusHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<LawyerApprovalStatusHistory> builder)
    {
        builder.ToTable("LawyerApprovalStatusHistory");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.OldStatus).HasConversion<int>().IsRequired();
        builder.Property(history => history.NewStatus).HasConversion<int>().IsRequired();
        builder.Property(history => history.Reason).HasMaxLength(1000);
        builder.HasOne(history => history.LawyerProfile).WithMany(profile => profile.StatusHistory)
            .HasForeignKey(history => history.LawyerProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(history => new { history.LawyerProfileId, history.ChangedOnUtc })
            .HasDatabaseName("IX_LawyerApprovalStatusHistory_LawyerProfileId_ChangedOnUtc");
    }
}
