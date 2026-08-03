namespace LawyerPlatform.Api.Contracts.Auth;

public sealed record RegisterAccountRequest(
    string FullName,
    string UserName,
    string Email,
    string PhoneNumber,
    string Password);
