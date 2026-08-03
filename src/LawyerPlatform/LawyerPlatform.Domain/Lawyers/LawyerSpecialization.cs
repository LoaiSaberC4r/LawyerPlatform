using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Domain.Lawyers;

public sealed class LawyerSpecialization
{
    private LawyerSpecialization()
    {
    }

    internal LawyerSpecialization(Guid lawyerProfileId, int legalSpecializationId)
    {
        LawyerProfileId = lawyerProfileId;
        LegalSpecializationId = legalSpecializationId;
    }

    public Guid LawyerProfileId { get; private set; }
    public LawyerProfile LawyerProfile { get; private set; } = null!;
    public int LegalSpecializationId { get; private set; }
    public LegalSpecialization LegalSpecialization { get; private set; } = null!;
}
