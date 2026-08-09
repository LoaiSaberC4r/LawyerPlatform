using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.ReferenceData;

public sealed class Area : AggregateRoot<int>, IAuditableEntity
{
    public const int MaximumNameLength = 150;
    private Area()
    {
    }

    private Area(int id, int cityId, string nameAr, string nameEn, int displayOrder)
        : base(id)
    {
        CityId = cityId;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public int CityId { get; private set; }
    public City City { get; private set; } = null!;
    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<Area> Create(int id, int cityId, string nameAr, string nameEn, int displayOrder)
    {
        if (!IsValid(id, cityId, nameAr, nameEn, displayOrder))
        {
            return Result<Area>.Fail(AreaErrors.Invalid);
        }

        return Result<Area>.Ok(new Area(id, cityId, nameAr, nameEn, displayOrder));
    }

    public Result Update(int cityId, string nameAr, string nameEn, int displayOrder)
    {
        if (!IsValid(Id, cityId, nameAr, nameEn, displayOrder)) return Result.Fail(AreaErrors.Invalid);
        CityId = cityId;
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DisplayOrder = displayOrder;
        return Result.Ok();
    }

    public Result Activate()
    {
        if (IsActive) return Result.Fail(AreaErrors.AlreadyActive);
        IsActive = true;
        return Result.Ok();
    }

    public Result Deactivate()
    {
        if (!IsActive) return Result.Fail(AreaErrors.AlreadyInactive);
        IsActive = false;
        return Result.Ok();
    }

    private static bool IsValid(int id, int cityId, string nameAr, string nameEn, int displayOrder)
        => id > 0 && cityId > 0 && displayOrder >= 0 && !string.IsNullOrWhiteSpace(nameAr) &&
           !string.IsNullOrWhiteSpace(nameEn) && nameAr.Trim().Length <= MaximumNameLength &&
           nameEn.Trim().Length <= MaximumNameLength;
}
