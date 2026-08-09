using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;

namespace LawyerPlatform.Application.Features.Clients.Profile;

public sealed record ClientAccountResponse(
    Guid Id,
    string UserName,
    string Email,
    string PhoneNumber,
    string Status);

public sealed record ClientOwnProfileResponse(
    Guid Id,
    string FullName,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    string RowVersion,
    ClientAccountResponse Account);

internal sealed record ClientOwnProfileSnapshot(
    Guid Id,
    string FullName,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    byte[] RowVersion,
    Guid UserAccountId,
    string UserName,
    string Email,
    string PhoneNumber,
    AccountStatus AccountStatus)
{
    public ClientOwnProfileResponse ToResponse()
        => new(
            Id,
            FullName,
            CreatedOnUtc,
            ModifiedOnUtc,
            RowVersionCodec.Encode(RowVersion),
            new ClientAccountResponse(
                UserAccountId,
                UserName,
                Email,
                PhoneNumber,
                AccountStatus.ToString()));
}

internal sealed class ClientOwnProfileSpecification
    : Specification<ClientProfile, ClientOwnProfileSnapshot>
{
    public ClientOwnProfileSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId);
        UseNoTracking();
        Select(profile => new ClientOwnProfileSnapshot(
            profile.Id,
            profile.FullName,
            profile.CreatedOnUtc,
            profile.ModifiedOnUtc,
            profile.RowVersion,
            profile.UserAccountId,
            profile.UserAccount.UserName,
            profile.UserAccount.Email,
            profile.UserAccount.PhoneNumber,
            profile.UserAccount.Status));
    }
}

internal sealed class ClientProfileByUserAccountIdSpecification : Specification<ClientProfile>
{
    public ClientProfileByUserAccountIdSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId);
        UseTracking();
    }
}
