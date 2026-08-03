namespace LawyerPlatform.Api.Contracts.Auth;

public sealed record LoginRequest(string UserNameOrEmail, string Password);
