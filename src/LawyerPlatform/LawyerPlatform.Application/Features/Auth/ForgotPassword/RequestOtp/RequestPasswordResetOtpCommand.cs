using BuildingBlock.Application.Abstraction;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.RequestOtp;

public sealed record RequestPasswordResetOtpCommand(string Email)
    : ICommand<RequestPasswordResetOtpResponse>;

public sealed record RequestPasswordResetOtpResponse(
    Guid RequestId,
    int ExpiresInSeconds,
    int ResendAfterSeconds);
