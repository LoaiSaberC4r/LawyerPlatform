using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminLawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Features.AdminLawyers.ReactivateLawyer;

public sealed record ReactivateLawyerCommand(Guid LawyerId, string RowVersion)
    : ICommand<AdminLawyerDecisionResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class ReactivateLawyerCommandValidator : AbstractValidator<ReactivateLawyerCommand>
{
    public ReactivateLawyerCommandValidator()
    {
        RuleFor(command => command.LawyerId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class ReactivateLawyerCommandHandler(AdminLawyerDecisionService service)
    : ICommandHandler<ReactivateLawyerCommand, AdminLawyerDecisionResponse>
{
    public Task<Result<AdminLawyerDecisionResponse>> Handle(ReactivateLawyerCommand command, CancellationToken cancellationToken)
        => service.ExecuteAsync(command.LawyerId, command.RowVersion, AdminLawyerDecision.Reactivate, null, cancellationToken);
}
