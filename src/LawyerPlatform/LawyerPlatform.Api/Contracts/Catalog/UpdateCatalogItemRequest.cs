namespace LawyerPlatform.Api.Contracts.Catalog;

public sealed record UpdateCatalogItemRequest(string Name, string? Description, decimal Price);
