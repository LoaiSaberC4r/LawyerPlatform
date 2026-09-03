namespace LawyerPlatform.Application.Abstractions.ReferenceData;

public interface IReferenceDataIdGenerator
{
    Task<int> NextGovernorateIdAsync(CancellationToken cancellationToken = default);

    Task<int> NextCityIdAsync(CancellationToken cancellationToken = default);

    Task<int> NextAreaIdAsync(CancellationToken cancellationToken = default);

    Task<int> NextLegalSpecializationIdAsync(CancellationToken cancellationToken = default);
}
