using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminLawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Features.AdminLawyers.SuspendLawyer;

public sealed record SuspendLawyerCommand(Guid LawyerId, string Reason, string RowVersion)
    : ICommand<AdminLawyerDecisionResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class SuspendLawyerCommandValidator : AbstractValidator<SuspendLawyerCommand>
{
    public SuspendLawyerCommandValidator()
    {
        RuleFor(command => command.LawyerId).NotEmpty();
        RuleFor(command => command.Reason)
            .NotEmpty().WithErrorCode("Lawyer.SuspensionReasonRequired")
            .MaximumLength(1000);
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class SuspendLawyerCommandHandler(AdminLawyerDecisionService service)
    : ICommandHandler<SuspendLawyerCommand, AdminLawyerDecisionResponse>
{
    public Task<Result<AdminLawyerDecisionResponse>> Handle(SuspendLawyerCommand command, CancellationToken cancellationToken)
        => service.ExecuteAsync(command.LawyerId, command.RowVersion, AdminLawyerDecision.Suspend, command.Reason, cancellationToken);
}
