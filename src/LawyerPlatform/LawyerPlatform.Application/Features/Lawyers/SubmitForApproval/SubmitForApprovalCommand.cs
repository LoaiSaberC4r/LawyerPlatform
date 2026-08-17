using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Application.Notifications.Email;

namespace LawyerPlatform.Application.Features.Lawyers.SubmitForApproval;

public sealed record SubmitForApprovalCommand(string RowVersion)
    : ICommand<SubmitForApprovalResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

public sealed record SubmitForApprovalResponse(string ApprovalStatus, DateTime SubmittedOnUtc, string RowVersion);

internal sealed class SubmitForApprovalCommandValidator : AbstractValidator<SubmitForApprovalCommand>
{
    public SubmitForApprovalCommandValidator()
    {
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class SubmitForApprovalCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager,
    LawyerAggregateCompletionService completionService,
    ILawyerDocumentPolicy documentPolicy,
    ILawyerAggregatePersistence aggregatePersistence,
    EmailNotificationCoordinator emailNotifications,
    IDateTimeProvider clock)
    : ICommandHandler<SubmitForApprovalCommand, SubmitForApprovalResponse>
{
    public async Task<Result<SubmitForApprovalResponse>> Handle(SubmitForApprovalCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<SubmitForApprovalResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var profile = await unitOfWork.WriteRepository<LawyerProfile>()
            .FirstOrDefaultAsync(new LawyerAggregateByUserAccountIdSpecification(userId), cancellationToken);
        if (profile is null)
        {
            return Result<SubmitForApprovalResponse>.Fail(LawyerErrors.NotFound);
        }

        if (profile.UserAccount.Status == AccountStatus.Suspended)
        {
            return Result<SubmitForApprovalResponse>.Fail(LawyerApplicationErrors.AccountSuspended);
        }

        if (profile.UserAccount.Status != AccountStatus.Active)
        {
            return Result<SubmitForApprovalResponse>.Fail(LawyerApplicationErrors.AccountInactive);
        }

        if (documentPolicy.RequiredDocumentTypes.Count == 0)
        {
            return Result<SubmitForApprovalResponse>.Fail(LawyerErrors.DocumentRequirementsNotConfigured);
        }

        var completion = await completionService.CalculateAsync(profile, cancellationToken);
        if (!completion.ProfileIsComplete)
        {
            return Result<SubmitForApprovalResponse>.Fail(LawyerErrors.ProfileIncomplete);
        }

        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure)
        {
            return Result<SubmitForApprovalResponse>.Fail(rowVersion.Errors);
        }

        concurrencyTokenManager.SetOriginalRowVersion(profile, rowVersion.Value);
        var transition = profile.SubmitForApproval(userId, true, true, clock.UtcNow);
        if (transition.IsFailure)
        {
            return Result<SubmitForApprovalResponse>.Fail(transition.Errors);
        }

        var history = profile.StatusHistory.Last();
        aggregatePersistence.Add(history);
        await emailNotifications.QueueLawyerTransitionAsync(profile, history, cancellationToken);

        profile.ModifiedOnUtc = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<SubmitForApprovalResponse>.Ok(new SubmitForApprovalResponse(
            profile.ApprovalStatus.ToString(),
            profile.SubmittedOnUtc!.Value,
            RowVersionCodec.Encode(profile.RowVersion)));
    }
}
