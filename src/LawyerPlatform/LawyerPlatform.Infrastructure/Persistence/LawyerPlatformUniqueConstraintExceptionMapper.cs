using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.Infrastructure.Persistence;

internal sealed class LawyerPlatformUniqueConstraintExceptionMapper : IExceptionToErrorMapper
{
    public bool TryMap(Exception exception, out Error error)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is DbUpdateConcurrencyException)
        {
            error = LawyerErrors.ConcurrencyConflict;
            return true;
        }

        if (exception is not DbUpdateException)
        {
            error = null!;
            return false;
        }

        var providerMessage = GetProviderMessages(exception);
        error = providerMessage switch
        {
            var message when message.Contains("UX_UserAccounts_NormalizedUserName", StringComparison.Ordinal) => AccountErrors.UserNameAlreadyExists,
            var message when message.Contains("UX_UserAccounts_NormalizedEmail", StringComparison.Ordinal) => AccountErrors.EmailAlreadyExists,
            var message when message.Contains("UX_UserAccounts_PhoneNumber", StringComparison.Ordinal) => AccountErrors.PhoneNumberAlreadyExists,
            var message when message.Contains("UX_ClientProfiles_UserAccountId", StringComparison.Ordinal) => Error.Conflict("ClientProfile.AccountAlreadyLinked", "The account already has a client profile."),
            var message when message.Contains("UX_LawyerProfiles_UserAccountId", StringComparison.Ordinal) => Error.Conflict("LawyerProfile.AccountAlreadyLinked", "The account already has a lawyer profile."),
            var message when message.Contains("UX_LawyerProfiles_ProfessionalRegistrationNumber", StringComparison.Ordinal) => LawyerErrors.RegistrationNumberAlreadyExists,
            var message when message.Contains("UX_LawyerOffices_LawyerProfileId_Primary", StringComparison.Ordinal) => Error.Conflict("Lawyer.OfficeEditNotAllowed", "A primary office already exists."),
            _ => null!
        };

        return error is not null;
    }

    private static string GetProviderMessages(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            messages.Add(current.Message);
        }

        return string.Join(' ', messages);
    }
}
