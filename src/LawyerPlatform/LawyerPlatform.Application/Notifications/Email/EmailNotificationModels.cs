namespace LawyerPlatform.Application.Notifications.Email;

using LawyerPlatform.Domain.Consultations;

public abstract record EmailNotificationModel;

public sealed record LawyerEmailNotificationModel(
    string LawyerName,
    Guid LawyerId,
    string? Reason = null) : EmailNotificationModel;

public sealed record ConsultationEmailNotificationModel(
    string RequesterName,
    string LawyerName,
    string ReferenceNumber,
    string SpecializationNameAr,
    string SpecializationNameEn,
    DateTime? PreferredAppointmentOnUtc = null,
    string? Reason = null,
    string? LawyerPublicPhoneNumber = null,
    string? LawyerOfficeMapUrl = null,
    ConsultationType ConsultationType = ConsultationType.Online,
    decimal? ConsultationPrice = null) : EmailNotificationModel;

public sealed record ClientEmailNotificationModel(
    string ClientName) : EmailNotificationModel;

public sealed record RegistrationWelcomeEmailNotificationModel(
    string FullName,
    string Email,
    string UserName) : EmailNotificationModel;

public sealed record EmailNotificationContent(
    string Subject,
    string HtmlBody);
