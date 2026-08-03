using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Features.Auth.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Auth.RegisterLawyer;

internal sealed class RegisterLawyerCommandHandler(
    IReadRepository<UserAccount, LawyerPlatformReadPersistence> accountReader,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IPasswordService passwordService,
    IAccountIdentifierNormalizer normalizer,
    IDateTimeProvider clock)
    : ICommandHandler<RegisterLawyerCommand, RegisterLawyerResponse>
{
    public async Task<Result<RegisterLawyerResponse>> Handle(RegisterLawyerCommand command, CancellationToken cancellationToken)
    {
        var normalizedUserName = normalizer.NormalizeUserName(command.UserName);
        var normalizedEmail = normalizer.NormalizeEmail(command.Email);
        var phoneNumber = command.PhoneNumber.Trim();

        var conflict = await FindConflictAsync(normalizedUserName, normalizedEmail, phoneNumber, cancellationToken);
        if (conflict is not null)
        {
            return Result<RegisterLawyerResponse>.Fail(conflict);
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
            return Result<RegisterLawyerResponse>.Fail(AuthErrors.PasswordHashingFailed);
        }

        var accountResult = UserAccount.CreateLawyer(
            command.UserName,
            normalizedUserName,
            command.Email,
            normalizedEmail,
            phoneNumber,
            passwordHash,
            clock.UtcNow);
        if (accountResult.IsFailure)
        {
            return Result<RegisterLawyerResponse>.Fail(accountResult.Errors);
        }

        var profileResult = LawyerProfile.Create(accountResult.Value, command.FullName);
        if (profileResult.IsFailure)
        {
            return Result<RegisterLawyerResponse>.Fail(profileResult.Errors);
        }

        await unitOfWork.WriteRepository<UserAccount>().AddAsync(accountResult.Value, cancellationToken);
        await unitOfWork.WriteRepository<LawyerProfile>().AddAsync(profileResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<RegisterLawyerResponse>.Ok(new RegisterLawyerResponse(
            accountResult.Value.Id,
            profileResult.Value.Id,
            accountResult.Value.UserName,
            accountResult.Value.Role.ToString(),
            accountResult.Value.Status.ToString(),
            profileResult.Value.ApprovalStatus.ToString()));
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
