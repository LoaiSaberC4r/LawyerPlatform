using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.ReferenceData;
using LawyerPlatform.Domain.Resources;
using Microsoft.EntityFrameworkCore;

namespace LawyerPlatform.Infrastructure.Persistence;

internal sealed class LawyerPlatformUniqueConstraintExceptionMapper : IExceptionToErrorMapper
{
    public bool TryMap(Exception exception, out Error error)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is DbUpdateConcurrencyException)
        {
            var concurrencyException = (DbUpdateConcurrencyException)exception;
            error = concurrencyException.Entries.Any(entry => entry.Entity is PasswordResetChallenge)
                ? PasswordResetErrors.ConcurrencyConflict
                : concurrencyException.Entries.Any(entry => entry.Entity is LawyerConsultationSettings)
                ? LawyerErrors.ConsultationSettingsConcurrencyConflict
                : concurrencyException.Entries.Any(entry => entry.Entity is Governorate)
                ? GovernorateErrors.ConcurrencyConflict
                : concurrencyException.Entries.Any(entry => entry.Entity is City)
                    ? CityErrors.ConcurrencyConflict
                    : concurrencyException.Entries.Any(entry => entry.Entity is Area)
                        ? AreaErrors.ConcurrencyConflict
                        : concurrencyException.Entries.Any(entry => entry.Entity is LegalSpecialization)
                            ? LegalSpecializationErrors.ConcurrencyConflict
                            : concurrencyException.Entries.Any(entry => entry.Entity is ConsultationRequest)
                                ? ConsultationRequestErrors.ConcurrencyConflict
                                : concurrencyException.Entries.Any(entry => entry.Entity is ClientProfile)
                                    ? ClientErrors.ConcurrencyConflict
                                    : concurrencyException.Entries.Any(entry => entry.Entity is UserAccount)
                                        ? AccountErrors.ConcurrencyConflict
                                        : LawyerErrors.ConcurrencyConflict;
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
            var message when message.Contains("UX_PasswordResetChallenges_Current_UserAccountId", StringComparison.Ordinal) => PasswordResetErrors.ConcurrencyConflict,
            var message when message.Contains("UX_ClientProfiles_UserAccountId", StringComparison.Ordinal) => Error.Conflict("ClientProfile.AccountAlreadyLinked", ErrorMessage.ClientProfileAlreadyLinked),
            var message when message.Contains("UX_LawyerProfiles_UserAccountId", StringComparison.Ordinal) => Error.Conflict("LawyerProfile.AccountAlreadyLinked", ErrorMessage.LawyerProfileAlreadyLinked),
            var message when message.Contains("UX_LawyerProfiles_ProfessionalRegistrationNumber", StringComparison.Ordinal) => LawyerErrors.RegistrationNumberAlreadyExists,
            var message when message.Contains("UX_LawyerConsultationSettings_LawyerProfileId", StringComparison.Ordinal) => LawyerErrors.ConsultationSettingsConcurrencyConflict,
            var message when message.Contains("UX_LawyerAvailabilities_SettingsId_DayOfWeek", StringComparison.Ordinal) => LawyerErrors.DuplicateAvailabilityDay,
            var message when message.Contains("UX_LawyerOffices_LawyerProfileId_Primary", StringComparison.Ordinal) => Error.Conflict("Lawyer.OfficeEditNotAllowed", ErrorMessage.PrimaryOfficeAlreadyExists),
            var message when message.Contains("UX_LegalSpecializations_NameAr", StringComparison.Ordinal) => LegalSpecializationErrors.DuplicateNameAr,
            var message when message.Contains("UX_LegalSpecializations_NameEn", StringComparison.Ordinal) => LegalSpecializationErrors.DuplicateNameEn,
            var message when message.Contains("UX_Governorates_NameAr", StringComparison.Ordinal) => GovernorateErrors.DuplicateNameAr,
            var message when message.Contains("UX_Governorates_NameEn", StringComparison.Ordinal) => GovernorateErrors.DuplicateNameEn,
            var message when message.Contains("UX_Cities_GovernorateId_NameAr", StringComparison.Ordinal) => CityErrors.DuplicateNameAr,
            var message when message.Contains("UX_Cities_GovernorateId_NameEn", StringComparison.Ordinal) => CityErrors.DuplicateNameEn,
            var message when message.Contains("UX_Areas_CityId_NameAr", StringComparison.Ordinal) => AreaErrors.DuplicateNameAr,
            var message when message.Contains("UX_Areas_CityId_NameEn", StringComparison.Ordinal) => AreaErrors.DuplicateNameEn,
            var message when message.Contains("UX_ConsultationRequests_ReferenceNumber", StringComparison.Ordinal) => ConsultationRequestErrors.ReferenceNumberConflict,
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
