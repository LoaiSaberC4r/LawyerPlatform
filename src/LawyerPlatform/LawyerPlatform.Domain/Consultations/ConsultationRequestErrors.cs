using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.Consultations;

public static class ConsultationRequestErrors
{
    public static readonly Error Invalid = Error.Validation(
        "ConsultationRequest.Invalid",
        "The consultation request is invalid.");

    public static readonly Error InvalidConsultationType = Error.Validation(
        "ConsultationRequest.InvalidConsultationType",
        "Consultation type must be Online or Onsite.");

    public static readonly Error ConsultationPriceRequired = Error.Validation(
        "ConsultationRequest.ConsultationPriceRequired",
        "An Online consultation requires a valid price snapshot.");

    public static readonly Error OnsitePriceNotAllowed = Error.Validation(
        "ConsultationRequest.OnsitePriceNotAllowed",
        "An Onsite consultation cannot contain a price snapshot.");

    public static readonly Error InvalidSource = Error.Domain(
        "ConsultationRequest.InvalidSource",
        "A consultation request must have exactly one valid requester source.");

    public static readonly Error InvalidStatusTransition = Error.Domain(
        "ConsultationRequest.InvalidStatusTransition",
        "The requested consultation status transition is not allowed.");

    public static readonly Error RejectionReasonRequired = Error.Validation(
        "ConsultationRequest.RejectionReasonRequired",
        "A rejection reason is required and cannot exceed 1000 characters.");

    public static readonly Error NotFound = Error.NotFound(
        "ConsultationRequest.NotFound",
        "The consultation request was not found.");

    public static readonly Error ConcurrencyConflict = Error.Conflict(
        "ConsultationRequest.ConcurrencyConflict",
        "The consultation request was changed by another request.");

    public static readonly Error InvalidRowVersion = Error.Validation(
        "ConsultationRequest.InvalidRowVersion",
        "RowVersion must be a valid Base64 value.");

    public static readonly Error LawyerUnavailable = Error.Domain(
        "ConsultationRequest.LawyerUnavailable",
        "The selected lawyer is not currently available for consultation requests.");

    public static readonly Error SpecializationNotOfferedByLawyer = Error.Domain(
        "ConsultationRequest.SpecializationNotOfferedByLawyer",
        "The selected specialization is not currently offered by the lawyer.");

    public static readonly Error PreferredAppointmentMustBeFuture = Error.Validation(
        "ConsultationRequest.PreferredAppointmentMustBeFuture",
        "The preferred appointment must be a future UTC time.");

    public static readonly Error LawyerAvailabilityNotConfigured = Error.Domain(
        "ConsultationRequest.LawyerAvailabilityNotConfigured",
        "The selected lawyer has not configured appointment availability.");

    public static readonly Error LawyerNotAvailableOnSelectedDay = Error.Domain(
        "ConsultationRequest.LawyerNotAvailableOnSelectedDay",
        "The selected lawyer is not available on the appointment day.");

    public static readonly Error OutsideLawyerWorkingHours = Error.Domain(
        "ConsultationRequest.OutsideLawyerWorkingHours",
        "The preferred appointment is outside the lawyer's working hours.");

    public static readonly Error ReferenceNumberConflict = Error.Conflict(
        "ConsultationRequest.ReferenceNumberConflict",
        "A unique consultation reference could not be allocated. Please retry.");

    public static readonly Error ReferenceVerificationFailed = Error.NotFound(
        "ConsultationRequest.ReferenceVerificationFailed",
        "The consultation request reference could not be verified.");
}
