using System.Globalization;
using System.Resources;

namespace LawyerPlatform.Domain.Resources;

public static class ErrorMessage
{
    private static readonly ResourceManager Manager = new(
        "LawyerPlatform.Domain.Resources.ErrorMessage",
        typeof(ErrorMessage).Assembly);

    public static string ValidationErrorTitle => GetString(nameof(ValidationErrorTitle));
    public static string DomainErrorTitle => GetString(nameof(DomainErrorTitle));
    public static string ResourceNotFoundTitle => GetString(nameof(ResourceNotFoundTitle));
    public static string ConflictTitle => GetString(nameof(ConflictTitle));
    public static string UnauthorizedTitle => GetString(nameof(UnauthorizedTitle));
    public static string SecurityErrorTitle => GetString(nameof(SecurityErrorTitle));
    public static string TooManyRequestsTitle => GetString(nameof(TooManyRequestsTitle));
    public static string InfrastructureErrorTitle => GetString(nameof(InfrastructureErrorTitle));
    public static string UnexpectedErrorTitle => GetString(nameof(UnexpectedErrorTitle));
    public static string MethodNotAllowedTitle => GetString(nameof(MethodNotAllowedTitle));
    public static string UnsupportedMediaTypeTitle => GetString(nameof(UnsupportedMediaTypeTitle));
    public static string AuthenticationRequired => GetString(nameof(AuthenticationRequired));
    public static string AccessForbidden => GetString(nameof(AccessForbidden));
    public static string RequestedResourceNotFound => GetString(nameof(RequestedResourceNotFound));
    public static string NotFound => RequestedResourceNotFound;
    public static string HttpMethodNotAllowed => GetString(nameof(HttpMethodNotAllowed));
    public static string UnsupportedContentType => GetString(nameof(UnsupportedContentType));
    public static string TooManyRequests => GetString(nameof(TooManyRequests));
    public static string UnknownErrorOccurred => GetString(nameof(UnknownErrorOccurred));
    public static string UnexpectedErrorOccurred => GetString(nameof(UnexpectedErrorOccurred));
    public static string RequestCouldNotBeCompleted => GetString(nameof(RequestCouldNotBeCompleted));
    public static string InvalidJson => GetString(nameof(InvalidJson));
    public static string InvalidInput => GetString(nameof(InvalidInput));
    public static string ConsultationRateLimitExceeded => GetString(nameof(ConsultationRateLimitExceeded));
    public static string AuthenticationRateLimitExceeded => GetString(nameof(AuthenticationRateLimitExceeded));
    public static string ContactInquiryRateLimitExceeded => GetString(nameof(ContactInquiryRateLimitExceeded));

    public static string ValidationNotNull => GetString(nameof(ValidationNotNull));
    public static string ValidationNotEmpty => GetString(nameof(ValidationNotEmpty));
    public static string ValidationEmail => GetString(nameof(ValidationEmail));
    public static string ValidationMaximumLength => GetString(nameof(ValidationMaximumLength));
    public static string ValidationMinimumLength => GetString(nameof(ValidationMinimumLength));
    public static string ValidationExactLength => GetString(nameof(ValidationExactLength));
    public static string ValidationLength => GetString(nameof(ValidationLength));
    public static string ValidationGreaterThan => GetString(nameof(ValidationGreaterThan));
    public static string ValidationGreaterThanOrEqual => GetString(nameof(ValidationGreaterThanOrEqual));
    public static string ValidationInclusiveBetween => GetString(nameof(ValidationInclusiveBetween));
    public static string ValidationEqual => GetString(nameof(ValidationEqual));
    public static string ValidationRegularExpression => GetString(nameof(ValidationRegularExpression));
    public static string ValidationPredicate => GetString(nameof(ValidationPredicate));
    public static string ValidationEnum => GetString(nameof(ValidationEnum));
    public static string ValidationNull => GetString(nameof(ValidationNull));
    public static string ValidationEmpty => GetString(nameof(ValidationEmpty));

    public static string AccountIdRequired => GetString(nameof(AccountIdRequired));
    public static string UserNameRequired => GetString(nameof(UserNameRequired));
    public static string UserNameInvalid => GetString(nameof(UserNameInvalid));
    public static string UserNameTooShort => GetString(nameof(UserNameTooShort));
    public static string UserNameTooLong => GetString(nameof(UserNameTooLong));
    public static string UserNameAlreadyExists => GetString(nameof(UserNameAlreadyExists));
    public static string EmailRequired => GetString(nameof(EmailRequired));
    public static string EmailInvalid => GetString(nameof(EmailInvalid));
    public static string EmailAlreadyExists => GetString(nameof(EmailAlreadyExists));
    public static string PhoneNumberRequired => GetString(nameof(PhoneNumberRequired));
    public static string PhoneNumberInvalid => GetString(nameof(PhoneNumberInvalid));
    public static string PhoneNumberAlreadyExists => GetString(nameof(PhoneNumberAlreadyExists));
    public static string PasswordRequired => GetString(nameof(PasswordRequired));
    public static string PasswordInvalid => GetString(nameof(PasswordInvalid));
    public static string NewPasswordRequired => GetString(nameof(NewPasswordRequired));
    public static string NewPasswordInvalid => GetString(nameof(NewPasswordInvalid));
    public static string FullNameRequired => GetString(nameof(FullNameRequired));
    public static string FullNameTooLong => GetString(nameof(FullNameTooLong));
    public static string AccountNotFound => GetString(nameof(AccountNotFound));
    public static string AccountSuspended => GetString(nameof(AccountSuspended));
    public static string AccountInactive => GetString(nameof(AccountInactive));
    public static string CurrentPasswordInvalid => GetString(nameof(CurrentPasswordInvalid));
    public static string PasswordMustBeDifferent => GetString(nameof(PasswordMustBeDifferent));
    public static string PasswordChangeRequired => GetString(nameof(PasswordChangeRequired));
    public static string PasswordHashRequired => GetString(nameof(PasswordHashRequired));
    public static string InvalidStatusTransition => GetString(nameof(InvalidStatusTransition));
    public static string InvalidRowVersion => GetString(nameof(InvalidRowVersion));
    public static string AccountConcurrencyConflict => GetString(nameof(AccountConcurrencyConflict));
    public static string CredentialVersionLimitReached => GetString(nameof(CredentialVersionLimitReached));
    public static string ClientProfileRequiresClientAccount => GetString(nameof(ClientProfileRequiresClientAccount));
    public static string LawyerProfileRequiresLawyerAccount => GetString(nameof(LawyerProfileRequiresLawyerAccount));
    public static string ClientProfileAlreadyLinked => GetString(nameof(ClientProfileAlreadyLinked));
    public static string LawyerProfileAlreadyLinked => GetString(nameof(LawyerProfileAlreadyLinked));

    public static string OtpInvalidOrExpired => GetString(nameof(OtpInvalidOrExpired));
    public static string ResetTokenInvalidOrExpired => GetString(nameof(ResetTokenInvalidOrExpired));
    public static string PasswordResetChallengeConsumed => GetString(nameof(PasswordResetChallengeConsumed));
    public static string PasswordResetConcurrencyConflict => GetString(nameof(PasswordResetConcurrencyConflict));
    public static string PasswordResetConfigurationInvalid => GetString(nameof(PasswordResetConfigurationInvalid));
    public static string PasswordRecoveryRateLimitExceeded => GetString(nameof(PasswordRecoveryRateLimitExceeded));
    public static string InvalidLogin => GetString(nameof(InvalidLogin));
    public static string PasswordCouldNotBeProcessed => GetString(nameof(PasswordCouldNotBeProcessed));

    public static string ClientNotFound => GetString(nameof(ClientNotFound));
    public static string ClientConcurrencyConflict => GetString(nameof(ClientConcurrencyConflict));
    public static string LawyerNotFound => GetString(nameof(LawyerNotFound));
    public static string LawyerProfileIncomplete => GetString(nameof(LawyerProfileIncomplete));
    public static string InvalidApprovalStatus => GetString(nameof(InvalidApprovalStatus));
    public static string LawyerProfileEditNotAllowed => GetString(nameof(LawyerProfileEditNotAllowed));
    public static string SensitiveProfileEditNotAllowed => GetString(nameof(SensitiveProfileEditNotAllowed));
    public static string ApprovalReasonRequired => GetString(nameof(ApprovalReasonRequired));
    public static string SuspensionReasonRequired => GetString(nameof(SuspensionReasonRequired));
    public static string RegistrationNumberAlreadyExists => GetString(nameof(RegistrationNumberAlreadyExists));
    public static string OfficeNotFound => GetString(nameof(OfficeNotFound));
    public static string OfficeEditNotAllowed => GetString(nameof(OfficeEditNotAllowed));
    public static string PrimaryOfficeAlreadyExists => GetString(nameof(PrimaryOfficeAlreadyExists));
    public static string InvalidLatitude => GetString(nameof(InvalidLatitude));
    public static string InvalidLongitude => GetString(nameof(InvalidLongitude));
    public static string InvalidOfficeCoordinates => GetString(nameof(InvalidOfficeCoordinates));
    public static string DuplicateSpecialization => GetString(nameof(DuplicateSpecialization));
    public static string DocumentNotFound => GetString(nameof(DocumentNotFound));
    public static string DocumentTypeRequired => GetString(nameof(DocumentTypeRequired));
    public static string InvalidDocumentType => GetString(nameof(InvalidDocumentType));
    public static string InvalidDocument => GetString(nameof(InvalidDocument));
    public static string RequiredDocumentCannotBeRemoved => GetString(nameof(RequiredDocumentCannotBeRemoved));
    public static string DocumentRequirementsNotConfigured => GetString(nameof(DocumentRequirementsNotConfigured));
    public static string ProfileImageNotFound => GetString(nameof(ProfileImageNotFound));
    public static string LawyerConcurrencyConflict => GetString(nameof(LawyerConcurrencyConflict));
    public static string ConsultationPriceInvalid => GetString(nameof(ConsultationPriceInvalid));
    public static string AvailabilityInvalid => GetString(nameof(AvailabilityInvalid));
    public static string DuplicateAvailabilityDay => GetString(nameof(DuplicateAvailabilityDay));
    public static string ConsultationSettingsInvalidRowVersion => GetString(nameof(ConsultationSettingsInvalidRowVersion));
    public static string ConsultationSettingsConcurrencyConflict => GetString(nameof(ConsultationSettingsConcurrencyConflict));
    public static string InvalidProfessionalProfile => GetString(nameof(InvalidProfessionalProfile));
    public static string InvalidOffice => GetString(nameof(InvalidOffice));

    public static string ConsultationRequestInvalid => GetString(nameof(ConsultationRequestInvalid));
    public static string InvalidConsultationType => GetString(nameof(InvalidConsultationType));
    public static string ConsultationPriceRequired => GetString(nameof(ConsultationPriceRequired));
    public static string OnsitePriceNotAllowed => GetString(nameof(OnsitePriceNotAllowed));
    public static string InvalidConsultationSource => GetString(nameof(InvalidConsultationSource));
    public static string InvalidConsultationStatusTransition => GetString(nameof(InvalidConsultationStatusTransition));
    public static string RejectionReasonRequired => GetString(nameof(RejectionReasonRequired));
    public static string ConsultationRequestNotFound => GetString(nameof(ConsultationRequestNotFound));
    public static string ConsultationRequestConcurrencyConflict => GetString(nameof(ConsultationRequestConcurrencyConflict));
    public static string LawyerUnavailable => GetString(nameof(LawyerUnavailable));
    public static string SpecializationNotOfferedByLawyer => GetString(nameof(SpecializationNotOfferedByLawyer));
    public static string PreferredAppointmentMustBeFuture => GetString(nameof(PreferredAppointmentMustBeFuture));
    public static string LawyerAvailabilityNotConfigured => GetString(nameof(LawyerAvailabilityNotConfigured));
    public static string LawyerNotAvailableOnSelectedDay => GetString(nameof(LawyerNotAvailableOnSelectedDay));
    public static string OutsideLawyerWorkingHours => GetString(nameof(OutsideLawyerWorkingHours));
    public static string ReferenceNumberConflict => GetString(nameof(ReferenceNumberConflict));
    public static string ReferenceVerificationFailed => GetString(nameof(ReferenceVerificationFailed));
    public static string CurrentClientProfileNotFound => GetString(nameof(CurrentClientProfileNotFound));

    public static string GovernorateInvalid => GetString(nameof(GovernorateInvalid));
    public static string GovernorateNotFound => GetString(nameof(GovernorateNotFound));
    public static string GovernorateDuplicateNameAr => GetString(nameof(GovernorateDuplicateNameAr));
    public static string GovernorateDuplicateNameEn => GetString(nameof(GovernorateDuplicateNameEn));
    public static string GovernorateAlreadyActive => GetString(nameof(GovernorateAlreadyActive));
    public static string GovernorateAlreadyInactive => GetString(nameof(GovernorateAlreadyInactive));
    public static string GovernorateConcurrencyConflict => GetString(nameof(GovernorateConcurrencyConflict));
    public static string CityInvalid => GetString(nameof(CityInvalid));
    public static string CityNotFound => GetString(nameof(CityNotFound));
    public static string CityDuplicateNameAr => GetString(nameof(CityDuplicateNameAr));
    public static string CityDuplicateNameEn => GetString(nameof(CityDuplicateNameEn));
    public static string InvalidGovernorate => GetString(nameof(InvalidGovernorate));
    public static string CityAlreadyActive => GetString(nameof(CityAlreadyActive));
    public static string CityAlreadyInactive => GetString(nameof(CityAlreadyInactive));
    public static string CityConcurrencyConflict => GetString(nameof(CityConcurrencyConflict));
    public static string AreaInvalid => GetString(nameof(AreaInvalid));
    public static string AreaNotFound => GetString(nameof(AreaNotFound));
    public static string AreaDuplicateNameAr => GetString(nameof(AreaDuplicateNameAr));
    public static string AreaDuplicateNameEn => GetString(nameof(AreaDuplicateNameEn));
    public static string InvalidCity => GetString(nameof(InvalidCity));
    public static string AreaAlreadyActive => GetString(nameof(AreaAlreadyActive));
    public static string AreaAlreadyInactive => GetString(nameof(AreaAlreadyInactive));
    public static string AreaConcurrencyConflict => GetString(nameof(AreaConcurrencyConflict));
    public static string ActiveLocationNotFound => GetString(nameof(ActiveLocationNotFound));
    public static string InvalidLocationHierarchy => GetString(nameof(InvalidLocationHierarchy));

    public static string LegalSpecializationInvalid => GetString(nameof(LegalSpecializationInvalid));
    public static string LegalSpecializationNotFound => GetString(nameof(LegalSpecializationNotFound));
    public static string LegalSpecializationDuplicateNameAr => GetString(nameof(LegalSpecializationDuplicateNameAr));
    public static string LegalSpecializationDuplicateNameEn => GetString(nameof(LegalSpecializationDuplicateNameEn));
    public static string LegalSpecializationAlreadyActive => GetString(nameof(LegalSpecializationAlreadyActive));
    public static string LegalSpecializationAlreadyInactive => GetString(nameof(LegalSpecializationAlreadyInactive));
    public static string LegalSpecializationConcurrencyConflict => GetString(nameof(LegalSpecializationConcurrencyConflict));
    public static string LegalSpecializationInactive => GetString(nameof(LegalSpecializationInactive));

    public static string ContactInquiryInvalid => GetString(nameof(ContactInquiryInvalid));
    public static string ContactInquiryNotFound => GetString(nameof(ContactInquiryNotFound));
    public static string InquiryTypeRequired => GetString(nameof(InquiryTypeRequired));
    public static string InquiryTypeTooLong => GetString(nameof(InquiryTypeTooLong));
    public static string MessageRequired => GetString(nameof(MessageRequired));
    public static string MessageTooLong => GetString(nameof(MessageTooLong));
    public static string PaginationPageNumberInvalid => GetString(nameof(PaginationPageNumberInvalid));
    public static string PaginationPageSizeInvalid => GetString(nameof(PaginationPageSizeInvalid));

    public static string GetString(string name, CultureInfo? culture = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Manager.GetString(name, culture ?? CultureInfo.CurrentUICulture)
            ?? throw new MissingManifestResourceException(
                $"The error message resource '{name}' is missing.");
    }
}
