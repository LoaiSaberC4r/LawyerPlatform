using BuildingBlock.Domain.EntitiesHelper;

namespace LawyerPlatform.Domain.Consultations;

public sealed class ConsultationRequestStatusHistory : Entity<Guid>
{
    private ConsultationRequestStatusHistory()
    {
    }

    internal ConsultationRequestStatusHistory(
        Guid consultationRequestId,
        ConsultationRequestStatus oldStatus,
        ConsultationRequestStatus newStatus,
        Guid changedByUserId,
        string? reason,
        DateTime changedOnUtc)
        : base(Guid.NewGuid())
    {
        ConsultationRequestId = consultationRequestId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        ChangedByUserId = changedByUserId;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ChangedOnUtc = changedOnUtc;
    }

    public Guid ConsultationRequestId { get; private set; }
    public ConsultationRequest ConsultationRequest { get; private set; } = null!;
    public ConsultationRequestStatus OldStatus { get; private set; }
    public ConsultationRequestStatus NewStatus { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ChangedOnUtc { get; private set; }
}
