using BuildingBlock.Application.Abstraction.Encryption;
using FluentValidation.Results;
using LawyerPlatform.Application.Features.Auth.RegisterClient;
using LawyerPlatform.Application.Features.Auth.RegisterLawyer;
using LawyerPlatform.Application.Features.ConsultationRequests.CreateGuest;
using LawyerPlatform.Application.Features.ConsultationRequests.PublicTracking;
using LawyerPlatform.Application.Features.Lawyers.UpsertPrimaryOffice;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.UnitTests.Validation;

public sealed class EgyptianMobileNumberValidationTests
{
    private readonly IPasswordService _passwordService = new TestPasswordService();

    public static TheoryData<string> ValidPhoneNumbers => new()
    {
        "01012345678",
        "01112345678",
        "01212345678",
        "01512345678"
    };

    public static TheoryData<string> InvalidPhoneNumbers => new()
    {
        "01312345678",
        "01412345678",
        "01612345678",
        "01712345678",
        "01812345678",
        "01912345678",
        "0101234567",
        "010123456789",
        "+201012345678",
        "00201012345678",
        "201012345678",
        "010 1234 5678",
        "010-1234-5678",
        "(010)12345678",
        "010ABC45678",
        "abcdefghijk",
        "12345678901"
    };

    [Theory]
    [MemberData(nameof(ValidPhoneNumbers))]
    public void CoveredValidators_AcceptValidEgyptianMobileNumbers(string phoneNumber)
    {
        Assert.True(ValidateClientRegistration(phoneNumber).IsValid);
        Assert.True(ValidateLawyerRegistration(phoneNumber).IsValid);
        Assert.True(ValidateGuestCreation(phoneNumber).IsValid);
        Assert.True(ValidateConsultationTracking(phoneNumber).IsValid);
        Assert.True(ValidateOffice(phoneNumber).IsValid);
    }

    [Theory]
    [MemberData(nameof(InvalidPhoneNumbers))]
    public void CoveredValidators_RejectInvalidPhoneNumbers(string phoneNumber)
    {
        AssertSinglePhoneError(
            ValidateClientRegistration(phoneNumber),
            nameof(RegisterClientCommand.PhoneNumber),
            "Account.PhoneNumberInvalid");
        AssertSinglePhoneError(
            ValidateLawyerRegistration(phoneNumber),
            nameof(RegisterLawyerCommand.PhoneNumber),
            "Account.PhoneNumberInvalid");
        AssertSinglePhoneError(
            ValidateGuestCreation(phoneNumber),
            nameof(CreateGuestConsultationRequestCommand.PhoneNumber),
            "ConsultationRequest.InvalidPhoneNumber");
        AssertSinglePhoneError(
            ValidateConsultationTracking(phoneNumber),
            nameof(TrackConsultationRequestQuery.PhoneNumber),
            "ConsultationRequest.InvalidPhoneNumber");
        AssertSinglePhoneError(
            ValidateOffice(phoneNumber),
            nameof(UpsertPrimaryOfficeCommand.PublicPhoneNumber),
            "Lawyer.InvalidPublicPhoneNumber");
    }

    [Fact]
    public void RequiredPhoneValidators_ReportOnlyRequiredErrorForEmptyInput()
    {
        AssertSinglePhoneError(
            ValidateClientRegistration(string.Empty),
            nameof(RegisterClientCommand.PhoneNumber),
            "Account.PhoneNumberRequired");
        AssertSinglePhoneError(
            ValidateLawyerRegistration(string.Empty),
            nameof(RegisterLawyerCommand.PhoneNumber),
            "Account.PhoneNumberRequired");
        AssertSinglePhoneError(
            ValidateGuestCreation(string.Empty),
            nameof(CreateGuestConsultationRequestCommand.PhoneNumber),
            "ConsultationRequest.PhoneNumberRequired");
        AssertSinglePhoneError(
            ValidateConsultationTracking(string.Empty),
            nameof(TrackConsultationRequestQuery.PhoneNumber),
            "ConsultationRequest.PhoneNumberRequired");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OfficeValidator_PreservesOptionalEmptyPhoneBehavior(string? phoneNumber)
    {
        Assert.True(ValidateOffice(phoneNumber).IsValid);
    }

    [Fact]
    public void GuestCreation_RejectsInternationalFormatAndKeepsLocalFormatValid()
    {
        AssertSinglePhoneError(
            ValidateGuestCreation("+201012345678"),
            nameof(CreateGuestConsultationRequestCommand.PhoneNumber),
            "ConsultationRequest.InvalidPhoneNumber");
        Assert.True(ValidateGuestCreation("01012345678").IsValid);
    }

    private ValidationResult ValidateClientRegistration(string phoneNumber)
        => new RegisterClientCommandValidator(_passwordService).Validate(
            new RegisterClientCommand(
                "Client One",
                "client.one",
                "client@example.test",
                phoneNumber,
                "StrongPassword1"));

    private ValidationResult ValidateLawyerRegistration(string phoneNumber)
        => new RegisterLawyerCommandValidator(_passwordService).Validate(
            new RegisterLawyerCommand(
                "Lawyer One",
                "lawyer.one",
                "lawyer@example.test",
                phoneNumber,
                "StrongPassword1"));

    private static ValidationResult ValidateGuestCreation(string phoneNumber)
        => new CreateGuestConsultationRequestCommandValidator().Validate(
            new CreateGuestConsultationRequestCommand(
                Guid.NewGuid(),
                ConsultationType.Online,
                1,
                "Guest One",
                phoneNumber,
                "guest@example.test",
                "Consultation description",
                null));

    private static ValidationResult ValidateConsultationTracking(string phoneNumber)
        => new TrackConsultationRequestQueryValidator().Validate(
            new TrackConsultationRequestQuery("AV-234567", phoneNumber));

    private static ValidationResult ValidateOffice(string? phoneNumber)
        => new UpsertPrimaryOfficeCommandValidator().Validate(
            new UpsertPrimaryOfficeCommand(1, 1, 1, "Office address", phoneNumber, null, null, null));

    private static void AssertSinglePhoneError(
        ValidationResult result,
        string propertyName,
        string expectedErrorCode)
    {
        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors, item => item.PropertyName == propertyName);
        Assert.Equal(expectedErrorCode, error.ErrorCode);
    }

    private sealed class TestPasswordService : IPasswordService
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);

        public PasswordVerification VerifyDetailed(string password, string passwordHash)
            => new(Verify(password, passwordHash), false);

        public Task<string> HashAsync(string password, CancellationToken ct = default)
            => Task.FromResult(Hash(password));

        public Task<bool> VerifyAsync(string password, string passwordHash, CancellationToken ct = default)
            => Task.FromResult(Verify(password, passwordHash));

        public bool IsStrongPassword(string password) => password == "StrongPassword1";
    }
}
