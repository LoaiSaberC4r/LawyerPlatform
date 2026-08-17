namespace LawyerPlatform.Application.Notifications.Email;

public interface IEmailNotificationFactory
{
    EmailNotificationContent Create(
        EmailNotificationType notificationType,
        EmailNotificationModel model);
}
