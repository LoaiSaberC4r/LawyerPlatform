using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Features.Auth.RegisterLawyer;

public sealed record RegisterLawyerCommand(
    string FullName,
    string UserName,
    string Email,
    string PhoneNumber,
    string Password)
    : ICommand<RegisterLawyerResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

public sealed record RegisterLawyerResponse(
    Guid UserAccountId,
    Guid LawyerProfileId,
    string UserName,
    string Role,
    string AccountStatus,
    string ApprovalStatus);
