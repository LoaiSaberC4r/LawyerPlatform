using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminLawyers.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;

namespace LawyerPlatform.Application.Features.AdminLawyers.RequestLawyerChanges;

public sealed record RequestLawyerChangesCommand(Guid LawyerId, string Explanation, string RowVersion)
    : ICommand<AdminLawyerDecisionResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class RequestLawyerChangesCommandValidator : AbstractValidator<RequestLawyerChangesCommand>
{
    public RequestLawyerChangesCommandValidator()
    {
        RuleFor(command => command.LawyerId).NotEmpty();
        RuleFor(command => command.Explanation)
            .NotEmpty().WithErrorCode("Lawyer.ApprovalReasonRequired")
            .MaximumLength(1000);
        RuleFor(command => command.RowVersion).Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode("Lawyer.InvalidRowVersion");
    }
}

internal sealed class RequestLawyerChangesCommandHandler(AdminLawyerDecisionService service)
    : ICommandHandler<RequestLawyerChangesCommand, AdminLawyerDecisionResponse>
{
    public Task<Result<AdminLawyerDecisionResponse>> Handle(RequestLawyerChangesCommand command, CancellationToken cancellationToken)
        => service.ExecuteAsync(command.LawyerId, command.RowVersion, AdminLawyerDecision.RequestChanges, command.Explanation, cancellationToken);
}
