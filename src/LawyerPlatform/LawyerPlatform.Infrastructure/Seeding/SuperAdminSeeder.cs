using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using LawyerPlatform.Application.Abstractions.Authentication;
using LawyerPlatform.Application.Abstractions.Seeding;
using LawyerPlatform.Application.Features.Auth.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed class SuperAdminSeeder(
    IReadRepository<UserAccount, LawyerPlatformReadPersistence> accountReader,
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IOptions<InitialSuperAdminOptions> options,
    IAccountIdentifierNormalizer normalizer,
    IPasswordService passwordService,
    IDateTimeProvider clock)
    : ISeeder
{
    public int ExecutionOrder => SeedingOrder.SuperAdmin;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var configured = options.Value;
        var existingSuperAdmin = await accountReader.GetByPropertyAsync(
            account => account.Role == AccountRole.SuperAdmin,
            cancellationToken);

        if (!configured.Enabled)
        {
            if (existingSuperAdmin is null)
            {
                throw new InvalidOperationException("Initial SuperAdmin seeding is disabled, but no SuperAdmin account exists.");
            }

            return;
        }

        var normalizedUserName = normalizer.NormalizeUserName(configured.UserName);
        var normalizedEmail = normalizer.NormalizeEmail(configured.Email);
        var phoneNumber = configured.PhoneNumber.Trim();
        var byId = await accountReader.GetByIdAsync(configured.Id, cancellationToken);
        var byUserName = await accountReader.GetByPropertyAsync(
            account => account.NormalizedUserName == normalizedUserName,
            cancellationToken);
        var byEmail = await accountReader.GetByPropertyAsync(
            account => account.NormalizedEmail == normalizedEmail,
            cancellationToken);
        var byPhone = await accountReader.GetByPropertyAsync(
            account => account.PhoneNumber == phoneNumber,
            cancellationToken);

        if (byId is not null)
        {
            if (byId.Role == AccountRole.SuperAdmin &&
                byId.NormalizedUserName == normalizedUserName &&
                byId.NormalizedEmail == normalizedEmail &&
                byId.PhoneNumber == phoneNumber)
            {
                return;
            }

            throw new InvalidOperationException("Initial SuperAdmin configuration conflicts with an existing account.");
        }

        if (existingSuperAdmin is not null ||
            IsDifferentAccount(byUserName, configured.Id) ||
            IsDifferentAccount(byEmail, configured.Id) ||
            IsDifferentAccount(byPhone, configured.Id))
        {
            throw new InvalidOperationException("Initial SuperAdmin configuration conflicts with an existing account.");
        }

        string passwordHash;
        try
        {
            passwordHash = await passwordService.HashAsync(configured.Password, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(AuthErrors.PasswordHashingFailed.Message, exception);
        }

        var accountResult = UserAccount.CreateSuperAdmin(
            configured.Id,
            configured.UserName,
            normalizedUserName,
            configured.Email,
            normalizedEmail,
            phoneNumber,
            passwordHash,
            clock.UtcNow);
        if (accountResult.IsFailure)
        {
            throw new InvalidOperationException("Initial SuperAdmin configuration is invalid.");
        }

        await unitOfWork.WriteRepository<UserAccount>().AddAsync(accountResult.Value, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool IsDifferentAccount(UserAccount? account, Guid configuredId)
        => account is not null && account.Id != configuredId;
}
