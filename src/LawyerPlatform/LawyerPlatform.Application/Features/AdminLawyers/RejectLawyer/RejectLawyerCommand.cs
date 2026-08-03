using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminLawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Features.AdminLawyers.RejectLawyer;

public sealed record RejectLawyerCommand(Guid LawyerId, string Reason, string RowVersion)
    : ICommand<AdminLawyerDecisionResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class RejectLawyerCommandValidator : AbstractValidator<RejectLawyerCommand>
{
    public RejectLawyerCommandValidator()
    {
        RuleFor(command => command.LawyerId).NotEmpty();
        RuleFor(command => command.Reason)
            .NotEmpty().WithErrorCode("Lawyer.ApprovalReasonRequired")
            .MaximumLength(1000);
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class RejectLawyerCommandHandler(AdminLawyerDecisionService service)
    : ICommandHandler<RejectLawyerCommand, AdminLawyerDecisionResponse>
{
    public Task<Result<AdminLawyerDecisionResponse>> Handle(RejectLawyerCommand command, CancellationToken cancellationToken)
        => service.ExecuteAsync(command.LawyerId, command.RowVersion, AdminLawyerDecision.Reject, command.Reason, cancellationToken);
}
