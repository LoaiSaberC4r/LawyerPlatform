using LawyerPlatform.Domain.ContactInquiries;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LawyerPlatform.IntegrationTests;

public sealed class ContactInquiryPersistenceModelTests
{
    [Fact]
    public void SqlServerModelContainsReadOnlyContactInquiryShapeAndPaginationIndex()
    {
        var options = new DbContextOptionsBuilder<LawyerPlatformDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=LawyerPlatformContactModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var context = new LawyerPlatformDbContext(options);
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ContactInquiry))!;

        Assert.Equal("ContactInquiries", entity.GetTableName());
        Assert.Equal(ContactInquiry.MaximumFullNameLength, entity.FindProperty(nameof(ContactInquiry.FullName))!.GetMaxLength());
        Assert.Equal(ContactInquiry.PhoneNumberLength, entity.FindProperty(nameof(ContactInquiry.PhoneNumber))!.GetMaxLength());
        Assert.Equal(ContactInquiry.MaximumEmailLength, entity.FindProperty(nameof(ContactInquiry.Email))!.GetMaxLength());
        Assert.Equal(ContactInquiry.MaximumInquiryTypeLength, entity.FindProperty(nameof(ContactInquiry.InquiryType))!.GetMaxLength());
        Assert.Equal(ContactInquiry.MaximumMessageLength, entity.FindProperty(nameof(ContactInquiry.Message))!.GetMaxLength());
        Assert.Null(entity.FindProperty("Status"));
        Assert.Null(entity.FindProperty("RowVersion"));
        Assert.Null(entity.FindProperty("IsDeleted"));

        var index = Assert.Single(
            entity.GetIndexes(),
            item => item.GetDatabaseName() == "IX_ContactInquiries_CreatedOnUtc_Id");
        Assert.Equal(
            [nameof(ContactInquiry.CreatedOnUtc), nameof(ContactInquiry.Id)],
            index.Properties.Select(property => property.Name));
        Assert.True(index.IsDescending?.All(descending => descending));
    }
}
