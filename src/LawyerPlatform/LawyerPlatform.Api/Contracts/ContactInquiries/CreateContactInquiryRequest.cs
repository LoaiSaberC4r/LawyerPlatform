namespace LawyerPlatform.Api.Contracts.ContactInquiries;

public sealed record CreateContactInquiryRequest(
    string FullName,
    string PhoneNumber,
    string Email,
    string InquiryType,
    string Message);
