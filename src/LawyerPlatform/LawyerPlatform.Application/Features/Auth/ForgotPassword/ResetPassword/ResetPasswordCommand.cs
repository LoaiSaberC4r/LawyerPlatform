using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Features.Auth.ForgotPassword.ResetPassword;

public sealed record ResetPasswordCommand(
    Guid RequestId,
    string ResetToken,
    string NewPassword,
    string ConfirmPassword)
    : ICommand<ResetPasswordResponse>,
      ITransactionalCommand<LawyerPlatformWritePersistence>;

public sealed record ResetPasswordResponse(bool PasswordReset);
