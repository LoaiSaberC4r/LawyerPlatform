namespace LawyerPlatform.Api.Contracts.Lawyers;

public sealed record LawyerDecisionRequest(string RowVersion);
public sealed record RejectLawyerRequest(string Reason, string RowVersion);
public sealed record RequestLawyerChangesRequest(string Explanation, string RowVersion);
public sealed record SuspendLawyerRequest(string Reason, string RowVersion);
