using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;

namespace LawyerPlatform.Application.Features.AdminClients.Common;

public sealed record AdminClientListItemResponse(
    Guid Id,
    string FullName,
    string UserName,
    string Email,
    string PhoneNumber,
    string AccountStatus,
    DateTime CreatedOnUtc,
    string AccountRowVersion);

public sealed record AdminClientAccountDetailsResponse(
    Guid Id,
    string UserName,
    string Email,
    string PhoneNumber,
    string Status,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    string RowVersion);

public sealed record AdminClientDetailsResponse(
    Guid Id,
    string FullName,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    string ProfileRowVersion,
    AdminClientAccountDetailsResponse Account);

public sealed record AdminClientLifecycleResponse(
    Guid ClientId,
    Guid UserAccountId,
    string AccountStatus,
    DateTime? ModifiedOnUtc,
    string RowVersion);

internal sealed record AdminClientListItemSnapshot(
    Guid Id,
    string FullName,
    string UserName,
    string Email,
    string PhoneNumber,
    AccountStatus AccountStatus,
    DateTime CreatedOnUtc,
    byte[] AccountRowVersion)
{
    public AdminClientListItemResponse ToResponse()
        => new(
            Id,
            FullName,
            UserName,
            Email,
            PhoneNumber,
            AccountStatus.ToString(),
            CreatedOnUtc,
            RowVersionCodec.Encode(AccountRowVersion));
}

internal sealed record AdminClientDetailsSnapshot(
    Guid Id,
    string FullName,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    byte[] ProfileRowVersion,
    Guid UserAccountId,
    string UserName,
    string Email,
    string PhoneNumber,
    AccountStatus AccountStatus,
    DateTime AccountCreatedOnUtc,
    DateTime? AccountModifiedOnUtc,
    byte[] AccountRowVersion)
{
    public AdminClientDetailsResponse ToResponse()
        => new(
            Id,
            FullName,
            CreatedOnUtc,
            ModifiedOnUtc,
            RowVersionCodec.Encode(ProfileRowVersion),
            new AdminClientAccountDetailsResponse(
                UserAccountId,
                UserName,
                Email,
                PhoneNumber,
                AccountStatus.ToString(),
                AccountCreatedOnUtc,
                AccountModifiedOnUtc,
                RowVersionCodec.Encode(AccountRowVersion)));
}

internal sealed class AdminClientsSpecification
    : Specification<ClientProfile, AdminClientListItemSnapshot>
{
    public AdminClientsSpecification(int pageNumber, int pageSize)
    {
        AddOrderByDescending(profile => profile.CreatedOnUtc);
        AddOrderBy(profile => profile.Id);
        ApplyPaging(pageNumber, pageSize, 100);
        UseNoTracking();
        Select(profile => new AdminClientListItemSnapshot(
            profile.Id,
            profile.FullName,
            profile.UserAccount.UserName,
            profile.UserAccount.Email,
            profile.UserAccount.PhoneNumber,
            profile.UserAccount.Status,
            profile.CreatedOnUtc,
            profile.UserAccount.RowVersion));
    }
}

internal sealed class AdminClientDetailsSpecification
    : Specification<ClientProfile, AdminClientDetailsSnapshot>
{
    public AdminClientDetailsSpecification(Guid clientId)
    {
        AddCriteria(profile => profile.Id == clientId);
        UseNoTracking();
        Select(profile => new AdminClientDetailsSnapshot(
            profile.Id,
            profile.FullName,
            profile.CreatedOnUtc,
            profile.ModifiedOnUtc,
            profile.RowVersion,
            profile.UserAccountId,
            profile.UserAccount.UserName,
            profile.UserAccount.Email,
            profile.UserAccount.PhoneNumber,
            profile.UserAccount.Status,
            profile.UserAccount.CreatedOnUtc,
            profile.UserAccount.ModifiedOnUtc,
            profile.UserAccount.RowVersion));
    }
}

internal sealed class AdminClientAggregateSpecification : Specification<ClientProfile>
{
    public AdminClientAggregateSpecification(Guid clientId)
    {
        AddCriteria(profile => profile.Id == clientId);
        AddInclude(profile => profile.UserAccount);
        UseTracking();
    }
}
