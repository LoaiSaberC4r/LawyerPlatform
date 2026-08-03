namespace LawyerPlatform.Application.Catalog;

public sealed record CatalogItemResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    DateTime CreatedOnUtc,
    DateTime? ModifiedOnUtc,
    byte[] RowVersion);
