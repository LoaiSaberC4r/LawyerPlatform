using LawyerPlatform.Domain.ContactInquiries;

namespace LawyerPlatform.UnitTests.ContactInquiries;

public sealed class ContactInquiryTests
{
    private static readonly DateTime CreatedOnUtc =
        new(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateValidInquirySucceedsAndNormalizesSafeValues()
    {
        var result = ContactInquiry.Create(
            "  Ahmed Mohamed  ",
            " 01012345678 ",
            " ahmed@example.test ",
            " Technical Support ",
            "  First line\nSecond line  ",
            CreatedOnUtc);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("Ahmed Mohamed", result.Value.FullName);
        Assert.Equal("01012345678", result.Value.PhoneNumber);
        Assert.Equal("ahmed@example.test", result.Value.Email);
        Assert.Equal("Technical Support", result.Value.InquiryType);
        Assert.Equal("First line\nSecond line", result.Value.Message);
        Assert.Equal(CreatedOnUtc, result.Value.CreatedOnUtc);
    }

    [Theory]
    [InlineData("fullName", "ContactInquiry.FullNameRequired")]
    [InlineData("phoneNumber", "ContactInquiry.PhoneNumberRequired")]
    [InlineData("email", "ContactInquiry.EmailRequired")]
    [InlineData("inquiryType", "ContactInquiry.InquiryTypeRequired")]
    [InlineData("message", "ContactInquiry.MessageRequired")]
    public void CreateRejectsRequiredWhitespaceOnlyValues(
        string field,
        string expectedCode)
    {
        var values = ValidValues();
        values[field] = "   ";

        var result = ContactInquiry.Create(
            values["fullName"],
            values["phoneNumber"],
            values["email"],
            values["inquiryType"],
            values["message"],
            CreatedOnUtc);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == expectedCode);
    }

    [Fact]
    public void InquiryTypeIsAnUnrestrictedStringRatherThanAnEnum()
    {
        const string frontendDefinedValue = "New Frontend Category 2027";

        var result = ContactInquiry.Create(
            "Ahmed Mohamed",
            "01112345678",
            "ahmed@example.test",
            frontendDefinedValue,
            "Please contact me.",
            CreatedOnUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(typeof(string), typeof(ContactInquiry).GetProperty(nameof(ContactInquiry.InquiryType))!.PropertyType);
        Assert.Equal(frontendDefinedValue, result.Value.InquiryType);
    }

    private static Dictionary<string, string> ValidValues()
        => new(StringComparer.Ordinal)
        {
            ["fullName"] = "Ahmed Mohamed",
            ["phoneNumber"] = "01012345678",
            ["email"] = "ahmed@example.test",
            ["inquiryType"] = "Technical Support",
            ["message"] = "Please contact me."
        };
}
