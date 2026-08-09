using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ConsultationRequestStatusHistoryConfiguration
    : IWriteEntityConfiguration<ConsultationRequestStatusHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<ConsultationRequestStatusHistory> builder)
    {
        builder.ToTable("ConsultationRequestStatusHistory");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.OldStatus).HasConversion<int>().IsRequired();
        builder.Property(history => history.NewStatus).HasConversion<int>().IsRequired();
        builder.Property(history => history.Reason).HasMaxLength(1000);
        builder.HasOne(history => history.ConsultationRequest)
            .WithMany(request => request.StatusHistory)
            .HasForeignKey(history => history.ConsultationRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(history => history.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(history => new { history.ConsultationRequestId, history.ChangedOnUtc })
            .HasDatabaseName("IX_ConsultationRequestStatusHistory_ConsultationRequestId_ChangedOnUtc");
    }
}
