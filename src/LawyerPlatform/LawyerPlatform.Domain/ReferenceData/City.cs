using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.ReferenceData;

public sealed class City : AggregateRoot<int>, IAuditableEntity
{
    public const int MaximumNameLength = 150;
    private City()
    {
    }

    private City(int id, int governorateId, string nameAr, string nameEn, int displayOrder)
        : base(id)
    {
        GovernorateId = governorateId;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public int GovernorateId { get; private set; }
    public Governorate Governorate { get; private set; } = null!;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<City> Create(int id, int governorateId, string nameAr, string nameEn, int displayOrder)
    {
        if (!IsValid(id, governorateId, nameAr, nameEn, displayOrder))
        {
            return Result<City>.Fail(CityErrors.Invalid);
        }

        return Result<City>.Ok(new City(id, governorateId, nameAr, nameEn, displayOrder));
    }

    public Result Update(int governorateId, string nameAr, string nameEn, int displayOrder)
    {
        if (!IsValid(Id, governorateId, nameAr, nameEn, displayOrder)) return Result.Fail(CityErrors.Invalid);
        GovernorateId = governorateId;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DisplayOrder = displayOrder;
        return Result.Ok();
    }

    public Result Activate()
    {
        if (IsActive) return Result.Fail(CityErrors.AlreadyActive);
        IsActive = true;
        return Result.Ok();
    }

    public Result Deactivate()
    {
        if (!IsActive) return Result.Fail(CityErrors.AlreadyInactive);
        IsActive = false;
        return Result.Ok();
    }

    private static bool IsValid(int id, int governorateId, string nameAr, string nameEn, int displayOrder)
        => id > 0 && governorateId > 0 && displayOrder >= 0 && !string.IsNullOrWhiteSpace(nameAr) &&
           !string.IsNullOrWhiteSpace(nameEn) && nameAr.Trim().Length <= MaximumNameLength &&
           nameEn.Trim().Length <= MaximumNameLength;
}
