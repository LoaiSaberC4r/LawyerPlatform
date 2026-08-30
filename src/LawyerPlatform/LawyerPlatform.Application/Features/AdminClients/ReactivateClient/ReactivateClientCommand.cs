using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminClients.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Application.Features.AdminClients.ReactivateClient;

public sealed record ReactivateClientCommand(Guid ClientId, string RowVersion)
    : ICommand<AdminClientLifecycleResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class ReactivateClientCommandValidator : AbstractValidator<ReactivateClientCommand>
{
    public ReactivateClientCommandValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty();
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode(AccountErrors.InvalidRowVersion.Code)
            .WithMessage(_ => ErrorMessage.InvalidRowVersion);
    }
}

internal sealed class ReactivateClientCommandHandler(AdminClientLifecycleService service)
    : ICommandHandler<ReactivateClientCommand, AdminClientLifecycleResponse>
{
    public Task<Result<AdminClientLifecycleResponse>> Handle(
        ReactivateClientCommand command,
        CancellationToken cancellationToken)
        => service.ExecuteAsync(
            command.ClientId,
            command.RowVersion,
            AdminClientLifecycleAction.Reactivate,
            cancellationToken);
}
