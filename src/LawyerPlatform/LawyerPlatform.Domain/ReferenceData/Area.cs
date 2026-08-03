using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.ReferenceData;

public sealed class Area : AggregateRoot<int>, IAuditableEntity
{
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
        if (id <= 0 || cityId <= 0 || string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            return Result<Area>.Fail(Error.Validation("Location.AreaInvalid", "Area seed data is invalid."));
        }

        return Result<Area>.Ok(new Area(id, cityId, nameAr, nameEn, displayOrder));
    }
}
