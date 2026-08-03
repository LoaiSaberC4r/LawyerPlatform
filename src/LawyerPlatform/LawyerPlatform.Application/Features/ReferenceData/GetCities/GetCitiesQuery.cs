using BuildingBlock.Application.Abstraction;

namespace LawyerPlatform.Application.Features.ReferenceData.GetCities;

public sealed record GetCitiesQuery(int GovernorateId) : IQuery<IReadOnlyList<ReferenceDataResponse>>;
