using BuildingBlock.Application.Abstraction;

namespace LawyerPlatform.Application.Features.ReferenceData.GetGovernorates;

public sealed record GetGovernoratesQuery : IQuery<IReadOnlyList<ReferenceDataResponse>>;
