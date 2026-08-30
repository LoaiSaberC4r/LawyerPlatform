using BuildingBlock.Infrastructure.Persistence;
using LawyerPlatform.Domain.ContactInquiries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LawyerPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ContactInquiryConfiguration
    : IWriteEntityConfiguration<ContactInquiry>
{
    public void ConfigureAggregate(EntityTypeBuilder<ContactInquiry> builder)
    {
        builder.ToTable("ContactInquiries");
        builder.HasKey(inquiry => inquiry.Id);
        builder.Property(inquiry => inquiry.FullName)
            .HasMaxLength(ContactInquiry.MaximumFullNameLength)
            .IsUnicode()
            .IsRequired();
        builder.Property(inquiry => inquiry.PhoneNumber)
            .HasMaxLength(ContactInquiry.PhoneNumberLength)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(inquiry => inquiry.Email)
            .HasMaxLength(ContactInquiry.MaximumEmailLength)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(inquiry => inquiry.InquiryType)
            .HasMaxLength(ContactInquiry.MaximumInquiryTypeLength)
            .IsUnicode()
            .IsRequired();
        builder.Property(inquiry => inquiry.Message)
            .HasMaxLength(ContactInquiry.MaximumMessageLength)
            .IsUnicode()
            .IsRequired();
        builder.Property(inquiry => inquiry.CreatedOnUtc).IsRequired();

        builder.HasIndex(inquiry => new { inquiry.CreatedOnUtc, inquiry.Id })
            .IsDescending(true, true)
            .HasDatabaseName("IX_ContactInquiries_CreatedOnUtc_Id");
    }
}
