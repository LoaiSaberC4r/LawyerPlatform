using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminLawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Features.AdminLawyers.ApproveLawyer;

public sealed record ApproveLawyerCommand(Guid LawyerId, string RowVersion)
    : ICommand<AdminLawyerDecisionResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class ApproveLawyerCommandValidator : AbstractValidator<ApproveLawyerCommand>
{
    public ApproveLawyerCommandValidator()
    {
        RuleFor(command => command.LawyerId).NotEmpty();
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class ApproveLawyerCommandHandler(AdminLawyerDecisionService service)
    : ICommandHandler<ApproveLawyerCommand, AdminLawyerDecisionResponse>
{
    public Task<Result<AdminLawyerDecisionResponse>> Handle(ApproveLawyerCommand command, CancellationToken cancellationToken)
        => service.ExecuteAsync(command.LawyerId, command.RowVersion, AdminLawyerDecision.Approve, null, cancellationToken);
}
