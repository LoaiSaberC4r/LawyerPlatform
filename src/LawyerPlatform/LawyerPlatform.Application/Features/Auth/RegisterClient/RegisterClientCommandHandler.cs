using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Features.Auth.Common;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;

namespace LawyerPlatform.Application.Features.Auth.RegisterClient;

internal sealed class RegisterClientCommandHandler(
    IReadRepository<UserAccount, LawyerPlatformReadPersistence> accountReader,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IPasswordService passwordService,
    IAccountIdentifierNormalizer normalizer,
    IDateTimeProvider clock,
    EmailNotificationCoordinator emailNotifications)
    : ICommandHandler<RegisterClientCommand, RegisterClientResponse>
{
    public async Task<Result<RegisterClientResponse>> Handle(RegisterClientCommand command, CancellationToken cancellationToken)
    {
        var normalizedUserName = normalizer.NormalizeUserName(command.UserName);
        var normalizedEmail = normalizer.NormalizeEmail(command.Email);
        var phoneNumber = command.PhoneNumber.Trim();

        var conflict = await FindConflictAsync(normalizedUserName, normalizedEmail, phoneNumber, cancellationToken);
        if (conflict is not null)
        {
            return Result<RegisterClientResponse>.Fail(conflict);
        }

        string passwordHash;
        try
        {
            passwordHash = await passwordService.HashAsync(command.Password, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Result<RegisterClientResponse>.Fail(AuthErrors.PasswordHashingFailed);
        }

        var accountResult = UserAccount.CreateClient(
            command.UserName,
            normalizedUserName,
            command.Email,
            normalizedEmail,
            phoneNumber,
            passwordHash,
            clock.UtcNow);
        if (accountResult.IsFailure)
        {
            return Result<RegisterClientResponse>.Fail(accountResult.Errors);
        }

        var profileResult = ClientProfile.Create(accountResult.Value, command.FullName);
        if (profileResult.IsFailure)
        {
            return Result<RegisterClientResponse>.Fail(profileResult.Errors);
        }

        await unitOfWork.WriteRepository<UserAccount>().AddAsync(accountResult.Value, cancellationToken);
        await unitOfWork.WriteRepository<ClientProfile>().AddAsync(profileResult.Value, cancellationToken);
        await emailNotifications.QueueClientRegistrationWelcomeAsync(profileResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<RegisterClientResponse>.Ok(new RegisterClientResponse(
            accountResult.Value.Id,
            profileResult.Value.Id,
            accountResult.Value.UserName,
            accountResult.Value.Role.ToString(),
            accountResult.Value.Status.ToString()));
    }

    private async Task<Error?> FindConflictAsync(
        string normalizedUserName,
        string normalizedEmail,
        string phoneNumber,
        CancellationToken cancellationToken)
    {
        if (await accountReader.AnyAsync(account => account.NormalizedUserName == normalizedUserName, cancellationToken))
        {
            return AccountErrors.UserNameAlreadyExists;
        }

        if (await accountReader.AnyAsync(account => account.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return AccountErrors.EmailAlreadyExists;
        }

        return await accountReader.AnyAsync(account => account.PhoneNumber == phoneNumber, cancellationToken)
            ? AccountErrors.PhoneNumberAlreadyExists
            : null;
    }
}
