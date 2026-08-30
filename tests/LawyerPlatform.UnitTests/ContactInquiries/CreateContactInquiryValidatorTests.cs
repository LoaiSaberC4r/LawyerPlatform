using System.Globalization;
using FluentValidation;
using LawyerPlatform.Application.Features.ContactInquiries.Create;
using LawyerPlatform.Domain.ContactInquiries;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.UnitTests.ContactInquiries;

[Collection(LawyerPlatform.UnitTests.Localization.CultureSensitiveGroup.Name)]
public sealed class CreateContactInquiryValidatorTests
{
    public static TheoryData<string> ValidEgyptianMobileNumbers => new()
    {
        "01012345678",
        "01112345678",
        "01212345678",
        "01512345678"
    };

    [Theory]
    [MemberData(nameof(ValidEgyptianMobileNumbers))]
    public void AcceptsEveryApprovedEgyptianMobilePrefix(string phoneNumber)
    {
        var result = new CreateContactInquiryCommandValidator()
            .Validate(ValidCommand() with { PhoneNumber = phoneNumber });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("0101234567")]
    [InlineData("010123456789")]
    [InlineData("01312345678")]
    [InlineData("+201012345678")]
    public void RejectsInvalidMobileLengthPrefixAndFormat(string phoneNumber)
    {
        var result = new CreateContactInquiryCommandValidator()
            .Validate(ValidCommand() with { PhoneNumber = phoneNumber });

        var failure = Assert.Single(
            result.Errors,
            error => error.PropertyName == nameof(CreateContactInquiryCommand.PhoneNumber));
        Assert.Equal("ContactInquiry.PhoneNumberInvalid", failure.ErrorCode);
    }

    [Fact]
    public void RejectsInvalidEmailEmptyInquiryTypeAndOversizedValues()
    {
        var command = ValidCommand() with
        {
            FullName = new string('a', ContactInquiry.MaximumFullNameLength + 1),
            Email = "not-an-email",
            InquiryType = "   ",
            Message = new string('m', ContactInquiry.MaximumMessageLength + 1)
        };

        var result = new CreateContactInquiryCommandValidator().Validate(command);

        Assert.Contains(result.Errors, error => error.ErrorCode == "ContactInquiry.FullNameTooLong");
        Assert.Contains(result.Errors, error => error.ErrorCode == "ContactInquiry.EmailInvalid");
        Assert.Contains(result.Errors, error => error.ErrorCode == "ContactInquiry.InquiryTypeRequired");
        Assert.Contains(result.Errors, error => error.ErrorCode == "ContactInquiry.MessageTooLong");
    }

    [Fact]
    public void AcceptsArbitraryBoundedInquiryTypeString()
    {
        var result = new CreateContactInquiryCommandValidator().Validate(
            ValidCommand() with { InquiryType = "Other / Suggestion from frontend" });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidationMessagesResolvePerCurrentUiCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar");
            var arabic = new CreateContactInquiryCommandValidator()
                .Validate(ValidCommand() with { Email = string.Empty });

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            var english = new CreateContactInquiryCommandValidator()
                .Validate(ValidCommand() with { Email = string.Empty });

            Assert.Equal(ErrorMessage.GetString(nameof(ErrorMessage.EmailRequired), CultureInfo.GetCultureInfo("ar")), arabic.Errors[0].ErrorMessage);
            Assert.Equal(ErrorMessage.GetString(nameof(ErrorMessage.EmailRequired), CultureInfo.GetCultureInfo("en")), english.Errors[0].ErrorMessage);
            Assert.Equal(arabic.Errors[0].ErrorCode, english.Errors[0].ErrorCode);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static CreateContactInquiryCommand ValidCommand()
        => new(
            "Ahmed Mohamed",
            "01012345678",
            "ahmed@example.test",
            "Technical Support",
            "Please contact me.");
}
