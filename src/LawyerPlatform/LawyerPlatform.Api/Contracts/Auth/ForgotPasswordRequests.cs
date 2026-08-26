namespace LawyerPlatform.Api.Contracts.Auth;

public sealed record RequestPasswordResetOtpRequest(string Email);

public sealed record VerifyPasswordResetOtpRequest(Guid RequestId, string Otp);

public sealed record ResetPasswordRequest(
    Guid RequestId,
    string ResetToken,
    string NewPassword,
    string ConfirmPassword);
