namespace LawyerPlatform.Application.Features.Dashboards;

public sealed record RequestDashboardResponse(
    long TotalRequests,
    long NewRequests,
    long UnderReviewRequests,
    long ApprovedRequests,
    long RejectedRequests,
    long CompletedRequests);
