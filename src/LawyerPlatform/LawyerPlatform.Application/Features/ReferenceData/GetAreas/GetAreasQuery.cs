using BuildingBlock.Application.Abstraction;

namespace LawyerPlatform.Application.Features.ReferenceData.GetAreas;

public sealed record GetAreasQuery(int CityId) : IQuery<IReadOnlyList<ReferenceDataResponse>>;
