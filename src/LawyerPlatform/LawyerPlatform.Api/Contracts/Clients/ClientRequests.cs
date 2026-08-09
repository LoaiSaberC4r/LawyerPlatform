namespace LawyerPlatform.Api.Contracts.Clients;

public sealed record UpdateClientProfileRequest(string FullName, string RowVersion);

public sealed record ChangeClientAccountStatusRequest(string RowVersion);
