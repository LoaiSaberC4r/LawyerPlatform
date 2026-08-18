using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.Consultations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ConsultationRequestConfiguration : IWriteEntityConfiguration<ConsultationRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<ConsultationRequest> builder)
    {
        builder.ToTable("ConsultationRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.ReferenceNumber)
            .HasMaxLength(ConsultationRequest.MaximumReferenceNumberLength)
            .IsRequired();
        builder.Property(request => request.GuestFullName).HasMaxLength(200);
        builder.Property(request => request.GuestPhoneNumber).HasMaxLength(30);
        builder.Property(request => request.GuestEmail).HasMaxLength(320);
        builder.Property(request => request.Description)
            .HasMaxLength(ConsultationRequest.MaximumDescriptionLength)
            .IsRequired();
        builder.Property(request => request.Status).HasConversion<int>().IsRequired();
        builder.Property(request => request.ConsultationType).HasConversion<int>().IsRequired();
        builder.Property(request => request.ConsultationPrice).HasPrecision(18, 2);
        builder.Property(request => request.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne(request => request.ClientProfile)
            .WithMany()
            .HasForeignKey(request => request.ClientProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(request => request.LawyerProfile)
            .WithMany()
            .HasForeignKey(request => request.LawyerProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(request => request.LegalSpecialization)
            .WithMany()
            .HasForeignKey(request => request.LegalSpecializationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(request => request.ReferenceNumber)
            .IsUnique()
            .HasDatabaseName("UX_ConsultationRequests_ReferenceNumber");
        builder.HasIndex(request => new { request.LawyerProfileId, request.Status, request.CreatedOnUtc })
            .HasDatabaseName("IX_ConsultationRequests_LawyerProfileId_Status_CreatedOnUtc");
        builder.HasIndex(request => new { request.ClientProfileId, request.CreatedOnUtc })
            .HasDatabaseName("IX_ConsultationRequests_ClientProfileId_CreatedOnUtc");
        builder.Navigation(request => request.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
