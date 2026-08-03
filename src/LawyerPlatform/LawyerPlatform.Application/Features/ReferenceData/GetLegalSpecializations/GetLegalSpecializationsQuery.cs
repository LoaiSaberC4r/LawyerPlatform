using BuildingBlock.Application.Abstraction;

namespace LawyerPlatform.Application.Features.ReferenceData.GetLegalSpecializations;

public sealed record GetLegalSpecializationsQuery : IQuery<IReadOnlyList<ReferenceDataResponse>>;
