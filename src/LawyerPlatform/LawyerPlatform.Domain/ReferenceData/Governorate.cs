using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.ReferenceData;

public sealed class Governorate : AggregateRoot<int>, IAuditableEntity
{
    public const int MaximumNameLength = 150;
    private Governorate()
    {
    }

    private Governorate(int id, string nameAr, string nameEn, int displayOrder)
        : base(id)
    {
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DisplayOrder = displayOrder;
        IsActive = true;
    }

    public string NameAr { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<Governorate> Create(int id, string nameAr, string nameEn, int displayOrder)
    {
        if (!IsValid(id, nameAr, nameEn, displayOrder))
        {
            return Result<Governorate>.Fail(GovernorateErrors.Invalid);
        }

        return Result<Governorate>.Ok(new Governorate(id, nameAr, nameEn, displayOrder));
    }

    public Result Update(string nameAr, string nameEn, int displayOrder)
    {
        if (!IsValid(Id, nameAr, nameEn, displayOrder)) return Result.Fail(GovernorateErrors.Invalid);
        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DisplayOrder = displayOrder;
        return Result.Ok();
    }

    public Result Activate()
    {
        if (IsActive) return Result.Fail(GovernorateErrors.AlreadyActive);
        IsActive = true;
        return Result.Ok();
    }

    public Result Deactivate()
    {
        if (!IsActive) return Result.Fail(GovernorateErrors.AlreadyInactive);
        IsActive = false;
        return Result.Ok();
    }

    private static bool IsValid(int id, string nameAr, string nameEn, int displayOrder)
        => id > 0 && displayOrder >= 0 && !string.IsNullOrWhiteSpace(nameAr) &&
           !string.IsNullOrWhiteSpace(nameEn) && nameAr.Trim().Length <= MaximumNameLength &&
           nameEn.Trim().Length <= MaximumNameLength;
}
