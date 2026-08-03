using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.Catalog;

public sealed class CatalogItem : AggregateRoot<Guid>, IAuditableEntity, ISoftDeleteEntity
{
    private CatalogItem()
    {
    }

    private CatalogItem(Guid id, string name, string? description, decimal price)
        : base(id)
    {
        Name = name;
        Description = description;
        Price = price;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public DateTime CreatedOnUtc { get; set; }

    public DateTime? ModifiedOnUtc { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedOnUtc { get; set; }

    public DateTime? RestoredOnUtc { get; set; }

    public byte[] RowVersion { get; private set; } = [];

    public static Result<CatalogItem> Create(string name, string? description, decimal price)
    {
        var validation = Validate(name, price);
        if (validation.IsFailure)
        {
            return Result<CatalogItem>.Fail(validation.Errors);
        }

        var item = new CatalogItem(
            Guid.NewGuid(),
            name.Trim(),
            NormalizeOptional(description),
            price);

        item.RaiseDomainEvent(new CatalogItemCreatedDomainEvent(item.Id));
        return Result<CatalogItem>.Ok(item);
    }

    public Result Update(string name, string? description, decimal price)
    {
        var validation = Validate(name, price);
        if (validation.IsFailure)
        {
            return validation;
        }

        Name = name.Trim();
        Description = NormalizeOptional(description);
        Price = price;
        return Result.Ok();
    }

    public Result Restore()
    {
        if (!IsDeleted)
        {
            return Result.Conflict(
                "CatalogItems.AlreadyActive",
                "The catalog item is not deleted.");
        }

        IsDeleted = false;
        return Result.Ok();
    }

    private static Result Validate(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Fail(Error.Validation(
                "CatalogItems.NameRequired",
                "Name is required."));
        }

        if (name.Trim().Length > 200)
        {
            return Result.Fail(Error.Validation(
                "CatalogItems.NameTooLong",
                "Name cannot exceed 200 characters."));
        }

        if (price < 0)
        {
            return Result.Fail(Error.Domain(
                "CatalogItems.PriceInvalid",
                "Price cannot be negative."));
        }

        return Result.Ok();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
