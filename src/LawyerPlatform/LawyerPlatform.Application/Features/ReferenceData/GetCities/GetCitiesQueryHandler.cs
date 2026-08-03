using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ReferenceData.GetCities;

internal sealed class GetCitiesQueryHandler(
    IReadRepository<Governorate, LawyerPlatformReadPersistence> governorates,
    IReadRepository<City, LawyerPlatformReadPersistence> cities)
    : IQueryHandler<GetCitiesQuery, IReadOnlyList<ReferenceDataResponse>>
{
    public async Task<Result<IReadOnlyList<ReferenceDataResponse>>> Handle(GetCitiesQuery query, CancellationToken cancellationToken)
    {
        var governorateIsActive = await governorates.AnyAsync(
            governorate => governorate.Id == query.GovernorateId && governorate.IsActive,
            cancellationToken);
        if (!governorateIsActive)
        {
            return Result<IReadOnlyList<ReferenceDataResponse>>.Fail(ReferenceDataErrors.GovernorateNotFound);
        }

        return Result<IReadOnlyList<ReferenceDataResponse>>.Ok(
            await cities.ListAsync(new ActiveCitiesByGovernorateSpec(query.GovernorateId), cancellationToken));
    }
}
