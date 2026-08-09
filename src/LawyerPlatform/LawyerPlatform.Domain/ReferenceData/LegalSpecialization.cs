using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.ReferenceData;

public sealed class LegalSpecialization : AggregateRoot<int>, IAuditableEntity
{
    private LegalSpecialization()
    {
    }

    private LegalSpecialization(int id, string nameAr, string nameEn, int displayOrder)
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

    public static Result<LegalSpecialization> Create(int id, string nameAr, string nameEn, int displayOrder)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(nameAr) || nameAr.Trim().Length > 150 ||
            string.IsNullOrWhiteSpace(nameEn) || nameEn.Trim().Length > 150 || displayOrder < 0)
        {
            return Result<LegalSpecialization>.Fail(LegalSpecializationErrors.Invalid);
        }

        return Result<LegalSpecialization>.Ok(new LegalSpecialization(id, nameAr, nameEn, displayOrder));
    }

    public Result Update(string nameAr, string nameEn, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(nameAr) || nameAr.Trim().Length > 150 ||
            string.IsNullOrWhiteSpace(nameEn) || nameEn.Trim().Length > 150 || displayOrder < 0)
        {
            return Result.Fail(LegalSpecializationErrors.Invalid);
        }

        NameAr = nameAr.Trim();
        NameEn = nameEn.Trim();
        DisplayOrder = displayOrder;
        return Result.Ok();
    }

    public Result Activate()
    {
        if (IsActive)
        {
            return Result.Fail(LegalSpecializationErrors.AlreadyActive);
        }

        IsActive = true;
        return Result.Ok();
    }

    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Fail(LegalSpecializationErrors.AlreadyInactive);
        }

        IsActive = false;
        return Result.Ok();
    }
}
