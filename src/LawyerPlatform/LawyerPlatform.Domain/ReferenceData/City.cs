using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.ReferenceData;

public sealed class City : AggregateRoot<int>, IAuditableEntity
{
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
        if (id <= 0 || governorateId <= 0 || string.IsNullOrWhiteSpace(nameAr) || string.IsNullOrWhiteSpace(nameEn))
        {
            return Result<City>.Fail(Error.Validation("Location.CityInvalid", "City seed data is invalid."));
        }

        return Result<City>.Ok(new City(id, governorateId, nameAr, nameEn, displayOrder));
    }
}
