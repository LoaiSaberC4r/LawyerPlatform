using System.Net.Mail;
using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Common;

namespace LawyerPlatform.Domain.ContactInquiries;

public sealed class ContactInquiry : AggregateRoot<Guid>
{
    public const int MaximumFullNameLength = 200;
    public const int MaximumEmailLength = 200;
    public const int MaximumInquiryTypeLength = 150;
    public const int MaximumMessageLength = 4000;
    public const int PhoneNumberLength = 11;

    private ContactInquiry()
    {
    }

    private ContactInquiry(
        Guid id,
        string fullName,
        string phoneNumber,
        string email,
        string inquiryType,
        string message,
        DateTime createdOnUtc)
        : base(id)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
        InquiryType = inquiryType;
        Message = message;
        CreatedOnUtc = createdOnUtc;
    }

    public string FullName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string InquiryType { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }

    public static Result<ContactInquiry> Create(
        string fullName,
        string phoneNumber,
        string email,
        string inquiryType,
        string message,
        DateTime createdOnUtc)
    {
        var normalizedFullName = fullName?.Trim() ?? string.Empty;
        var normalizedPhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        var normalizedEmail = email?.Trim() ?? string.Empty;
        var normalizedInquiryType = inquiryType?.Trim() ?? string.Empty;
        var normalizedMessage = message?.Trim() ?? string.Empty;
        var errors = new List<Error>();

        AddRequiredOrLengthError(
            normalizedFullName,
            MaximumFullNameLength,
            ContactInquiryErrors.FullNameRequired,
            ContactInquiryErrors.FullNameTooLong,
            errors);

        if (normalizedPhoneNumber.Length == 0)
        {
            errors.Add(ContactInquiryErrors.PhoneNumberRequired);
        }
        else if (!EgyptianMobileNumber.IsValid(normalizedPhoneNumber))
        {
            errors.Add(ContactInquiryErrors.PhoneNumberInvalid);
        }

        if (normalizedEmail.Length == 0)
        {
            errors.Add(ContactInquiryErrors.EmailRequired);
        }
        else if (normalizedEmail.Length > MaximumEmailLength ||
                 !MailAddress.TryCreate(normalizedEmail, out var parsedEmail) ||
                 !string.Equals(parsedEmail.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(ContactInquiryErrors.EmailInvalid);
        }

        AddRequiredOrLengthError(
            normalizedInquiryType,
            MaximumInquiryTypeLength,
            ContactInquiryErrors.InquiryTypeRequired,
            ContactInquiryErrors.InquiryTypeTooLong,
            errors);
        AddRequiredOrLengthError(
            normalizedMessage,
            MaximumMessageLength,
            ContactInquiryErrors.MessageRequired,
            ContactInquiryErrors.MessageTooLong,
            errors);

        if (createdOnUtc == default)
        {
            errors.Add(ContactInquiryErrors.Invalid);
        }

        return errors.Count > 0
            ? Result<ContactInquiry>.Fail(errors)
            : Result<ContactInquiry>.Ok(new ContactInquiry(
                Guid.NewGuid(),
                normalizedFullName,
                normalizedPhoneNumber,
                normalizedEmail,
                normalizedInquiryType,
                normalizedMessage,
                RequireUtc(createdOnUtc)));
    }

    private static void AddRequiredOrLengthError(
        string value,
        int maximumLength,
        Error requiredError,
        Error tooLongError,
        List<Error> errors)
    {
        if (value.Length == 0)
        {
            errors.Add(requiredError);
        }
        else if (value.Length > maximumLength)
        {
            errors.Add(tooLongError);
        }
    }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
