using LawyerPlatform.Application.Notifications.Email;

namespace LawyerPlatform.Infrastructure.Email;

public sealed class EmailOutboxMessage
{
    private EmailOutboxMessage()
    {
    }

    internal EmailOutboxMessage(
        Guid id,
        EmailNotificationType notificationType,
        Guid aggregateId,
        string idempotencyKey,
        string recipientEmail,
        string subject,
        string htmlBody,
        DateTime createdOnUtc)
    {
        Id = id;
        NotificationType = notificationType;
        AggregateId = aggregateId;
        IdempotencyKey = idempotencyKey;
        RecipientEmail = recipientEmail;
        Subject = subject;
        HtmlBody = htmlBody;
        CreatedOnUtc = createdOnUtc;
        Status = EmailOutboxStatus.Pending;
    }

    public Guid Id { get; private set; }
    public EmailNotificationType NotificationType { get; private set; }
    public Guid AggregateId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RecipientEmail { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string HtmlBody { get; private set; } = string.Empty;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ProcessedOnUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextAttemptOnUtc { get; private set; }
    public string? LastError { get; private set; }
    public EmailOutboxStatus Status { get; private set; }
    public Guid? ProcessingToken { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
}
