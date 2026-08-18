namespace LawyerPlatform.Application.Notifications.Email;

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
    string? LawyerOfficeMapUrl = null) : EmailNotificationModel;

public sealed record ClientEmailNotificationModel(
    string ClientName) : EmailNotificationModel;

public sealed record EmailNotificationContent(
    string Subject,
    string HtmlBody);
