using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Application.Notifications.Email;

namespace LawyerPlatform.Application.Features.AdminLawyers.Common;

internal enum AdminLawyerDecision
{
    Approve,
    Reject,
    RequestChanges,
    Suspend,
    Reactivate
}

public sealed record AdminLawyerDecisionResponse(
    Guid LawyerId,
    string ApprovalStatus,
    string? Reason,
    DateTime? ApprovedOnUtc,
    DateTime? SuspendedOnUtc,
    string RowVersion);

internal sealed class AdminLawyerDecisionService(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager,
    LawyerAggregateCompletionService completionService,
    ILawyerDocumentPolicy documentPolicy,
    ILawyerAggregatePersistence aggregatePersistence,
    EmailNotificationCoordinator emailNotifications,
    IDateTimeProvider clock)
{
    public async Task<Result<AdminLawyerDecisionResponse>> ExecuteAsync(
        Guid lawyerId,
        string rowVersionValue,
        AdminLawyerDecision decision,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } actorId)
        {
            return Result<AdminLawyerDecisionResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var profile = await unitOfWork.WriteRepository<LawyerProfile>()
            .FirstOrDefaultAsync(new LawyerAggregateByIdSpecification(lawyerId), cancellationToken);
        if (profile is null)
        {
            return Result<AdminLawyerDecisionResponse>.Fail(LawyerErrors.NotFound);
        }

        var rowVersion = RowVersionCodec.Decode(rowVersionValue);
        if (rowVersion.IsFailure)
        {
            return Result<AdminLawyerDecisionResponse>.Fail(rowVersion.Errors);
        }

        var requiresCompletion = decision is AdminLawyerDecision.Approve or AdminLawyerDecision.Reactivate;
        var complete = true;
        if (requiresCompletion)
        {
            if (documentPolicy.RequiredDocumentTypes.Count == 0)
            {
                return Result<AdminLawyerDecisionResponse>.Fail(LawyerErrors.DocumentRequirementsNotConfigured);
            }

            complete = (await completionService.CalculateAsync(profile, cancellationToken)).ProfileIsComplete;
        }

        concurrencyTokenManager.SetOriginalRowVersion(profile, rowVersion.Value);
        var nowUtc = clock.UtcNow;
        var transition = decision switch
        {
            AdminLawyerDecision.Approve => profile.Approve(actorId, complete, nowUtc),
            AdminLawyerDecision.Reject => profile.Reject(actorId, reason ?? string.Empty, nowUtc),
            AdminLawyerDecision.RequestChanges => profile.RequestChanges(actorId, reason ?? string.Empty, nowUtc),
            AdminLawyerDecision.Suspend => profile.Suspend(actorId, reason ?? string.Empty, nowUtc),
            AdminLawyerDecision.Reactivate => profile.Reactivate(actorId, complete, nowUtc),
            _ => Result.Fail(LawyerErrors.InvalidApprovalStatus)
        };
        if (transition.IsFailure)
        {
            return Result<AdminLawyerDecisionResponse>.Fail(transition.Errors);
        }

        var history = profile.StatusHistory.Last();
        aggregatePersistence.Add(history);
        await emailNotifications.QueueLawyerTransitionAsync(profile, history, cancellationToken);

        profile.ModifiedOnUtc = nowUtc;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var currentReason = profile.ApprovalStatus == LawyerApprovalStatus.Suspended
            ? profile.SuspensionReason
            : profile.ApprovalReason;
        return Result<AdminLawyerDecisionResponse>.Ok(new AdminLawyerDecisionResponse(
            profile.Id,
            profile.ApprovalStatus.ToString(),
            currentReason,
            profile.ApprovedOnUtc,
            profile.SuspendedOnUtc,
            RowVersionCodec.Encode(profile.RowVersion)));
    }
}
