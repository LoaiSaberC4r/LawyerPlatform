using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Domain.Consultations;

public static class ConsultationRequestErrors
{
    public static Error Invalid => Error.Validation(
        "ConsultationRequest.Invalid",
        ErrorMessage.ConsultationRequestInvalid);

    public static Error InvalidConsultationType => Error.Validation(
        "ConsultationRequest.InvalidConsultationType",
        ErrorMessage.InvalidConsultationType);

    public static Error ConsultationPriceRequired => Error.Validation(
        "ConsultationRequest.ConsultationPriceRequired",
        ErrorMessage.ConsultationPriceRequired);

    public static Error OnsitePriceNotAllowed => Error.Validation(
        "ConsultationRequest.OnsitePriceNotAllowed",
        ErrorMessage.OnsitePriceNotAllowed);

    public static Error InvalidSource => Error.Domain(
        "ConsultationRequest.InvalidSource",
        ErrorMessage.InvalidConsultationSource);

    public static Error InvalidStatusTransition => Error.Domain(
        "ConsultationRequest.InvalidStatusTransition",
        ErrorMessage.InvalidConsultationStatusTransition);

    public static Error RejectionReasonRequired => Error.Validation(
        "ConsultationRequest.RejectionReasonRequired",
        ErrorMessage.RejectionReasonRequired);

    public static Error NotFound => Error.NotFound(
        "ConsultationRequest.NotFound",
        ErrorMessage.ConsultationRequestNotFound);

    public static Error ConcurrencyConflict => Error.Conflict(
        "ConsultationRequest.ConcurrencyConflict",
        ErrorMessage.ConsultationRequestConcurrencyConflict);

    public static Error InvalidRowVersion => Error.Validation(
        "ConsultationRequest.InvalidRowVersion",
        ErrorMessage.InvalidRowVersion);

    public static Error LawyerUnavailable => Error.Domain(
        "ConsultationRequest.LawyerUnavailable",
        ErrorMessage.LawyerUnavailable);

    public static Error SpecializationNotOfferedByLawyer => Error.Domain(
        "ConsultationRequest.SpecializationNotOfferedByLawyer",
        ErrorMessage.SpecializationNotOfferedByLawyer);

    public static Error PreferredAppointmentMustBeFuture => Error.Validation(
        "ConsultationRequest.PreferredAppointmentMustBeFuture",
        ErrorMessage.PreferredAppointmentMustBeFuture);

    public static Error LawyerAvailabilityNotConfigured => Error.Domain(
        "ConsultationRequest.LawyerAvailabilityNotConfigured",
        ErrorMessage.LawyerAvailabilityNotConfigured);

    public static Error LawyerNotAvailableOnSelectedDay => Error.Domain(
        "ConsultationRequest.LawyerNotAvailableOnSelectedDay",
        ErrorMessage.LawyerNotAvailableOnSelectedDay);

    public static Error OutsideLawyerWorkingHours => Error.Domain(
        "ConsultationRequest.OutsideLawyerWorkingHours",
        ErrorMessage.OutsideLawyerWorkingHours);

    public static Error ReferenceNumberConflict => Error.Conflict(
        "ConsultationRequest.ReferenceNumberConflict",
        ErrorMessage.ReferenceNumberConflict);

    public static Error ReferenceVerificationFailed => Error.NotFound(
        "ConsultationRequest.ReferenceVerificationFailed",
        ErrorMessage.ReferenceVerificationFailed);
}
