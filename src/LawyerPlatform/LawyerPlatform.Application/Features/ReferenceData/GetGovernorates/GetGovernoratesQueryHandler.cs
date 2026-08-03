using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ReferenceData.GetGovernorates;

internal sealed class GetGovernoratesQueryHandler(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetGovernoratesQuery, IReadOnlyList<ReferenceDataResponse>>
{
    public async Task<Result<IReadOnlyList<ReferenceDataResponse>>> Handle(GetGovernoratesQuery query, CancellationToken cancellationToken)
        => Result<IReadOnlyList<ReferenceDataResponse>>.Ok(
            await repository.ListAsync(new ActiveGovernoratesSpec(), cancellationToken));
}
