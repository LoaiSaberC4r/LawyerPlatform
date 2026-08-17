namespace LawyerPlatform.Application.Notifications.Email;

public enum EmailNotificationType
{
    LawyerSubmittedForApproval = 1,
    LawyerApproved = 2,
    LawyerChangesRequested = 3,
    LawyerRejected = 4,
    LawyerSuspended = 5,
    LawyerReactivated = 6,
    ConsultationRequestCreatedForLawyer = 7,
    ConsultationRequestCreatedConfirmation = 8,
    ConsultationUnderReview = 9,
    ConsultationApproved = 10,
    ConsultationRejected = 11,
    ConsultationCompleted = 12,
    ClientSuspended = 13,
    ClientReactivated = 14
}
