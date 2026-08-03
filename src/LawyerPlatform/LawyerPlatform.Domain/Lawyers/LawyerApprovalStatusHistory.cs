using BuildingBlock.Domain.EntitiesHelper;

namespace LawyerPlatform.Domain.Lawyers;

public sealed class LawyerApprovalStatusHistory : Entity<Guid>
{
    private LawyerApprovalStatusHistory()
    {
    }

    internal LawyerApprovalStatusHistory(
        Guid lawyerProfileId,
        LawyerApprovalStatus oldStatus,
        LawyerApprovalStatus newStatus,
        Guid changedByUserId,
        string? reason,
        DateTime changedOnUtc)
        : base(Guid.NewGuid())
    {
        LawyerProfileId = lawyerProfileId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        ChangedByUserId = changedByUserId;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ChangedOnUtc = changedOnUtc;
    }

    public Guid LawyerProfileId { get; private set; }
    public LawyerProfile LawyerProfile { get; private set; } = null!;
    public LawyerApprovalStatus OldStatus { get; private set; }
    public LawyerApprovalStatus NewStatus { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime ChangedOnUtc { get; private set; }
}
