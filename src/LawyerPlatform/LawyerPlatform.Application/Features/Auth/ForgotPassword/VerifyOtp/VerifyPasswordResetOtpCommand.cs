using BuildingBlock.Application.Abstraction;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.VerifyOtp;

public sealed record VerifyPasswordResetOtpCommand(Guid RequestId, string Otp)
    : ICommand<VerifyPasswordResetOtpResponse>;

public sealed record VerifyPasswordResetOtpResponse(
    string ResetToken,
    int ExpiresInSeconds);
