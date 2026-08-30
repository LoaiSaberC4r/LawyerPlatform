using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminClients.Common;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Application.Features.AdminClients.SuspendClient;

public sealed record SuspendClientCommand(Guid ClientId, string RowVersion)
    : ICommand<AdminClientLifecycleResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class SuspendClientCommandValidator : AbstractValidator<SuspendClientCommand>
{
    public SuspendClientCommandValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty();
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode(AccountErrors.InvalidRowVersion.Code)
            .WithMessage(_ => ErrorMessage.InvalidRowVersion);
    }
}

internal sealed class SuspendClientCommandHandler(AdminClientLifecycleService service)
    : ICommandHandler<SuspendClientCommand, AdminClientLifecycleResponse>
{
    public Task<Result<AdminClientLifecycleResponse>> Handle(
        SuspendClientCommand command,
        CancellationToken cancellationToken)
        => service.ExecuteAsync(
            command.ClientId,
            command.RowVersion,
            AdminClientLifecycleAction.Suspend,
            cancellationToken);
}
