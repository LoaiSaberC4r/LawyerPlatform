using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using LawyerPlatform.Application.Features.Auth.Common;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Features.Auth.ChangePassword;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword)
    : ICommand<AuthTokenResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;
