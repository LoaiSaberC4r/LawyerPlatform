using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Domain.ContactInquiries;

public static class ContactInquiryErrors
{
    public static Error Invalid => Error.Validation(
        "ContactInquiry.Invalid",
        ErrorMessage.ContactInquiryInvalid);

    public static Error NotFound => Error.NotFound(
        "ContactInquiry.NotFound",
        ErrorMessage.ContactInquiryNotFound);

    public static Error FullNameRequired => Error.Validation(
        "ContactInquiry.FullNameRequired",
        ErrorMessage.FullNameRequired);

    public static Error FullNameTooLong => Error.Validation(
        "ContactInquiry.FullNameTooLong",
        ErrorMessage.FullNameTooLong);

    public static Error PhoneNumberRequired => Error.Validation(
        "ContactInquiry.PhoneNumberRequired",
        ErrorMessage.PhoneNumberRequired);

    public static Error PhoneNumberInvalid => Error.Validation(
        "ContactInquiry.PhoneNumberInvalid",
        ErrorMessage.PhoneNumberInvalid);

    public static Error EmailRequired => Error.Validation(
        "ContactInquiry.EmailRequired",
        ErrorMessage.EmailRequired);

    public static Error EmailInvalid => Error.Validation(
        "ContactInquiry.EmailInvalid",
        ErrorMessage.EmailInvalid);

    public static Error InquiryTypeRequired => Error.Validation(
        "ContactInquiry.InquiryTypeRequired",
        ErrorMessage.InquiryTypeRequired);

    public static Error InquiryTypeTooLong => Error.Validation(
        "ContactInquiry.InquiryTypeTooLong",
        ErrorMessage.InquiryTypeTooLong);

    public static Error MessageRequired => Error.Validation(
        "ContactInquiry.MessageRequired",
        ErrorMessage.MessageRequired);

    public static Error MessageTooLong => Error.Validation(
        "ContactInquiry.MessageTooLong",
        ErrorMessage.MessageTooLong);
}
