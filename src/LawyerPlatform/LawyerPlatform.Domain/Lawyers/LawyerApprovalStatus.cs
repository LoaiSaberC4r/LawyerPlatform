namespace LawyerPlatform.Domain.Lawyers;

public enum LawyerApprovalStatus
{
    Draft = 1,
    PendingApproval = 2,
    ChangesRequested = 3,
    Approved = 4,
    Rejected = 5,
    Suspended = 6
}
