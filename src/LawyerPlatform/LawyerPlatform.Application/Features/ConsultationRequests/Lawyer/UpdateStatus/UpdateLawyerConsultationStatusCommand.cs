using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Abstractions.Consultations;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Application.Notifications.Email;

namespace LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.UpdateStatus;

public sealed record UpdateLawyerConsultationStatusCommand(
    Guid RequestId,
    ConsultationRequestStatus Status,
    string? Reason,
    string RowVersion)
    : ICommand<UpdateLawyerConsultationStatusResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

public sealed record UpdateLawyerConsultationStatusResponse(
    Guid Id,
    string Status,
    string? RejectionReason,
    DateTime? CompletedOnUtc,
    string RowVersion);

internal sealed class UpdateLawyerConsultationStatusCommandValidator
    : AbstractValidator<UpdateLawyerConsultationStatusCommand>
{
    public UpdateLawyerConsultationStatusCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();
        RuleFor(command => command.Status).IsInEnum();
        RuleFor(command => command.Reason).MaximumLength(1000);
        RuleFor(command => command.Reason)
            .NotEmpty()
            .When(command => command.Status == ConsultationRequestStatus.Rejected)
            .WithErrorCode("ConsultationRequest.RejectionReasonRequired");
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("ConsultationRequest.InvalidRowVersion");
    }
}

internal sealed class UpdateLawyerConsultationStatusCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IConcurrencyTokenManager concurrencyTokenManager,
    IConsultationAggregatePersistence aggregatePersistence,
    EmailNotificationCoordinator emailNotifications,
    IDateTimeProvider clock)
    : ICommandHandler<UpdateLawyerConsultationStatusCommand, UpdateLawyerConsultationStatusResponse>
{
    public async Task<Result<UpdateLawyerConsultationStatusResponse>> Handle(
        UpdateLawyerConsultationStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<UpdateLawyerConsultationStatusResponse>.Fail(ConsultationRequestErrors.NotFound);
        }

        var request = await unitOfWork.WriteRepository<ConsultationRequest>()
            .FirstOrDefaultAsync(
                new OwnedConsultationRequestSpecification(userId, command.RequestId),
                cancellationToken);
        if (request is null)
        {
            return Result<UpdateLawyerConsultationStatusResponse>.Fail(ConsultationRequestErrors.NotFound);
        }

        var rowVersion = RowVersionCodec.Decode(command.RowVersion);
        if (rowVersion.IsFailure)
        {
            return Result<UpdateLawyerConsultationStatusResponse>.Fail(
                ConsultationRequestErrors.InvalidRowVersion);
        }

        concurrencyTokenManager.SetOriginalRowVersion(request, rowVersion.Value);
        var change = request.ChangeStatus(command.Status, userId, command.Reason, clock.UtcNow);
        if (change.IsFailure)
        {
            return Result<UpdateLawyerConsultationStatusResponse>.Fail(change.Errors);
        }

        var history = request.StatusHistory.Last();
        aggregatePersistence.Add(history);
        await emailNotifications.QueueConsultationTransitionAsync(request, history, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<UpdateLawyerConsultationStatusResponse>.Ok(
            new UpdateLawyerConsultationStatusResponse(
                request.Id,
                request.Status.ToString(),
                request.Status == ConsultationRequestStatus.Rejected ? command.Reason?.Trim() : null,
                request.CompletedOnUtc,
                RowVersionCodec.Encode(request.RowVersion)));
    }
}

internal sealed class OwnedConsultationRequestSpecification : Specification<ConsultationRequest>
{
    public OwnedConsultationRequestSpecification(Guid userAccountId, Guid requestId)
    {
        AddCriteria(request =>
            request.Id == requestId && request.LawyerProfile.UserAccountId == userAccountId);
        UseTracking();
    }
}
