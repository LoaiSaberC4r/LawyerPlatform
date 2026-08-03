using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ReferenceData.GetLegalSpecializations;

internal sealed class GetLegalSpecializationsQueryHandler(
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetLegalSpecializationsQuery, IReadOnlyList<ReferenceDataResponse>>
{
    public async Task<Result<IReadOnlyList<ReferenceDataResponse>>> Handle(GetLegalSpecializationsQuery query, CancellationToken cancellationToken)
        => Result<IReadOnlyList<ReferenceDataResponse>>.Ok(
            await repository.ListAsync(new ActiveLegalSpecializationsSpec(), cancellationToken));
}
