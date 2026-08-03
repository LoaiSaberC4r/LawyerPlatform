using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Features.Auth.RegisterClient;

public sealed record RegisterClientCommand(
    string FullName,
    string UserName,
    string Email,
    string PhoneNumber,
    string Password)
    : ICommand<RegisterClientResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

public sealed record RegisterClientResponse(
    Guid UserAccountId,
    Guid ClientProfileId,
    string UserName,
    string Role,
    string Status);
