using System.Net.Mail;
using BuildingBlock.Application.Time;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LawyerPlatform.Infrastructure.Email;

internal sealed partial class EmailNotificationOutbox(
    LawyerPlatformDbContext dbContext,
    IDateTimeProvider clock,
    ILogger<EmailNotificationOutbox> logger)
    : IEmailNotificationOutbox
{
    public async Task QueueAsync(
        QueueEmailNotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSafeRecipient(notification.RecipientEmail))
        {
            InvalidRecipientSkipped(logger, notification.NotificationType, notification.AggregateId);
            return;
        }

        ValidateNotification(notification);
        var alreadyTracked = dbContext.EmailOutboxMessages.Local.Any(
            message => message.IdempotencyKey == notification.IdempotencyKey);
        if (alreadyTracked || await dbContext.EmailOutboxMessages
                .AsNoTracking()
                .AnyAsync(
                    message => message.IdempotencyKey == notification.IdempotencyKey,
                    cancellationToken))
        {
            return;
        }

        dbContext.EmailOutboxMessages.Add(new EmailOutboxMessage(
            Guid.NewGuid(),
            notification.NotificationType,
            notification.AggregateId,
            notification.IdempotencyKey,
            notification.RecipientEmail.Trim(),
            notification.Subject,
            notification.HtmlBody,
            RequireUtc(clock.UtcNow)));
        NotificationQueued(logger, notification.NotificationType, notification.AggregateId);
    }

    private static void ValidateNotification(QueueEmailNotification notification)
    {
        if (notification.AggregateId == Guid.Empty ||
            string.IsNullOrWhiteSpace(notification.IdempotencyKey) ||
            notification.IdempotencyKey.Length > 450 ||
            string.IsNullOrWhiteSpace(notification.Subject) ||
            notification.Subject.Length > 500 ||
            notification.Subject.Contains('\r', StringComparison.Ordinal) ||
            notification.Subject.Contains('\n', StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(notification.HtmlBody))
        {
            throw new InvalidOperationException("The email notification is invalid.");
        }
    }

    private static bool IsSafeRecipient(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 320 ||
            value.Contains('\r', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal) ||
            !MailAddress.TryCreate(value.Trim(), out var parsed))
        {
            return false;
        }

        return string.Equals(parsed.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    [LoggerMessage(
        EventId = 4300,
        Level = LogLevel.Information,
        Message = "Email notification {NotificationType} queued for aggregate {AggregateId}.")]
    private static partial void NotificationQueued(
        ILogger logger,
        EmailNotificationType notificationType,
        Guid aggregateId);

    [LoggerMessage(
        EventId = 4301,
        Level = LogLevel.Warning,
        Message = "Email notification {NotificationType} for aggregate {AggregateId} was skipped because its persisted recipient is invalid.")]
    private static partial void InvalidRecipientSkipped(
        ILogger logger,
        EmailNotificationType notificationType,
        Guid aggregateId);
}
