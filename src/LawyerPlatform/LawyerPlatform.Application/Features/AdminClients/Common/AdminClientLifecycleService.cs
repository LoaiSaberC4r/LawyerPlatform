using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;

namespace LawyerPlatform.Application.Features.AdminClients.Common;

internal enum AdminClientLifecycleAction
{
    Suspend,
    Reactivate
}

internal sealed class AdminClientLifecycleService(
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager,
    IDateTimeProvider clock)
{
    public async Task<Result<AdminClientLifecycleResponse>> ExecuteAsync(
        Guid clientId,
        string rowVersionValue,
        AdminClientLifecycleAction action,
        CancellationToken cancellationToken)
    {
        var profile = await unitOfWork.WriteRepository<ClientProfile>()
            .FirstOrDefaultAsync(new AdminClientAggregateSpecification(clientId), cancellationToken);
        if (profile is null || profile.UserAccount.Role != AccountRole.Client)
        {
            return Result<AdminClientLifecycleResponse>.Fail(ClientErrors.NotFound);
        }

        if (!RowVersionCodec.TryDecode(rowVersionValue, out var rowVersion))
        {
            return Result<AdminClientLifecycleResponse>.Fail(AccountErrors.InvalidRowVersion);
        }

        var account = profile.UserAccount;
        concurrencyTokenManager.SetOriginalRowVersion(account, rowVersion);
        var transition = action switch
        {
            AdminClientLifecycleAction.Suspend => account.Suspend(clock.UtcNow),
            AdminClientLifecycleAction.Reactivate => account.Reactivate(clock.UtcNow),
            _ => Result.Fail(AccountErrors.InvalidStatusTransition)
        };
        if (transition.IsFailure)
        {
            return Result<AdminClientLifecycleResponse>.Fail(transition.Errors);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<AdminClientLifecycleResponse>.Ok(new AdminClientLifecycleResponse(
            profile.Id,
            account.Id,
            account.Status.ToString(),
            account.ModifiedOnUtc,
            RowVersionCodec.Encode(account.RowVersion)));
    }
}
