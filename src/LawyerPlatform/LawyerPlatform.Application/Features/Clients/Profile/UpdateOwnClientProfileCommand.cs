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
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Application.Features.Clients.Profile;

public sealed record UpdateOwnClientProfileCommand(string FullName, string RowVersion)
    : ICommand<ClientOwnProfileResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class UpdateOwnClientProfileCommandValidator
    : AbstractValidator<UpdateOwnClientProfileCommand>
{
    public UpdateOwnClientProfileCommandValidator()
    {
        RuleFor(command => command.FullName)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode(AccountErrors.FullNameRequired.Code)
            .WithMessage(_ => ErrorMessage.FullNameRequired);
        RuleFor(command => command.FullName)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= 200)
            .WithErrorCode(AccountErrors.FullNameTooLong.Code)
            .WithMessage(_ => ErrorMessage.FullNameTooLong);
        RuleFor(command => command.RowVersion)
            .Must(value => RowVersionCodec.TryDecode(value, out _))
            .WithErrorCode(ClientErrors.InvalidRowVersion.Code)
            .WithMessage(_ => ErrorMessage.InvalidRowVersion);
    }
}

internal sealed class UpdateOwnClientProfileCommandHandler(
    ICurrentUser currentUser,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IReadRepository<ClientProfile, LawyerPlatformReadPersistence> reader,
    IConcurrencyTokenManager concurrencyTokenManager,
    IDateTimeProvider clock)
    : ICommandHandler<UpdateOwnClientProfileCommand, ClientOwnProfileResponse>
{
    public async Task<Result<ClientOwnProfileResponse>> Handle(
        UpdateOwnClientProfileCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<ClientOwnProfileResponse>.Fail(ClientErrors.NotFound);
        }

        var profile = await unitOfWork.WriteRepository<ClientProfile>()
            .FirstOrDefaultAsync(new ClientProfileByUserAccountIdSpecification(userId), cancellationToken);
        if (profile is null)
        {
            return Result<ClientOwnProfileResponse>.Fail(ClientErrors.NotFound);
        }

        if (!RowVersionCodec.TryDecode(command.RowVersion, out var rowVersion))
        {
            return Result<ClientOwnProfileResponse>.Fail(ClientErrors.InvalidRowVersion);
        }

        concurrencyTokenManager.SetOriginalRowVersion(profile, rowVersion);
        var update = profile.UpdateFullName(command.FullName);
        if (update.IsFailure)
        {
            return Result<ClientOwnProfileResponse>.Fail(update.Errors);
        }

        profile.ModifiedOnUtc = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var snapshot = await reader.FirstOrDefaultAsync(
            new ClientOwnProfileSpecification(userId),
            cancellationToken);
        return snapshot is null
            ? Result<ClientOwnProfileResponse>.Fail(ClientErrors.NotFound)
            : Result<ClientOwnProfileResponse>.Ok(snapshot.ToResponse());
    }
}
