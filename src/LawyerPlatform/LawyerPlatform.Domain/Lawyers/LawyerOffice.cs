using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Domain.Lawyers;

public sealed class LawyerOffice : Entity<Guid>, IAuditableEntity
{
    private LawyerOffice()
    {
    }

    internal LawyerOffice(
        Guid lawyerProfileId,
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        string? publicPhoneNumber)
        : base(Guid.NewGuid())
    {
        LawyerProfileId = lawyerProfileId;
        Update(governorateId, cityId, areaId, detailedAddress, publicPhoneNumber);
        IsPrimary = true;
        IsActive = true;
    }

    public Guid LawyerProfileId { get; private set; }
    public LawyerProfile LawyerProfile { get; private set; } = null!;
    public int GovernorateId { get; private set; }
    public Governorate Governorate { get; private set; } = null!;
    public int CityId { get; private set; }
    public City City { get; private set; } = null!;
    public int AreaId { get; private set; }
    public Area Area { get; private set; } = null!;
    public string DetailedAddress { get; private set; } = string.Empty;
    public string? PublicPhoneNumber { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    internal void Update(
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        string? publicPhoneNumber)
    {
        GovernorateId = governorateId;
        CityId = cityId;
        AreaId = areaId;
        DetailedAddress = detailedAddress.Trim();
        PublicPhoneNumber = string.IsNullOrWhiteSpace(publicPhoneNumber) ? null : publicPhoneNumber.Trim();
        IsPrimary = true;
        IsActive = true;
    }
}
