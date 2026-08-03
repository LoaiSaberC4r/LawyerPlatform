using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.ReferenceData;

public sealed class Governorate : AggregateRoot<int>, IAuditableEntity
{
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
        if (id <= 0 || string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            return Result<Governorate>.Fail(Error.Validation("Location.GovernorateInvalid", "Governorate seed data is invalid."));
        }

        return Result<Governorate>.Ok(new Governorate(id, nameAr, nameEn, displayOrder));
    }
}
