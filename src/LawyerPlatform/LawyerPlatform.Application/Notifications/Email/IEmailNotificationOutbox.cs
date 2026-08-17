namespace LawyerPlatform.Application.Notifications.Email;

public sealed record QueueEmailNotification(
    EmailNotificationType NotificationType,
    Guid AggregateId,
    string IdempotencyKey,
    string RecipientEmail,
    string Subject,
    string HtmlBody);

public interface IEmailNotificationOutbox
{
    Task QueueAsync(QueueEmailNotification notification, CancellationToken cancellationToken);
}
