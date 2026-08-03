using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.ReferenceData.GetAreas;

internal sealed class GetAreasQueryHandler(
    IReadRepository<City, LawyerPlatformReadPersistence> cities,
    IReadRepository<Area, LawyerPlatformReadPersistence> areas)
    : IQueryHandler<GetAreasQuery, IReadOnlyList<ReferenceDataResponse>>
{
    public async Task<Result<IReadOnlyList<ReferenceDataResponse>>> Handle(GetAreasQuery query, CancellationToken cancellationToken)
    {
        var hierarchy = await cities.FirstOrDefaultAsync(new CityHierarchySpec(query.CityId), cancellationToken);
        if (hierarchy is null)
        {
            return Result<IReadOnlyList<ReferenceDataResponse>>.Fail(ReferenceDataErrors.CityNotFound);
        }

        if (!hierarchy.CityIsActive || !hierarchy.GovernorateIsActive)
        {
            return Result<IReadOnlyList<ReferenceDataResponse>>.Fail(ReferenceDataErrors.InvalidHierarchy);
        }

        return Result<IReadOnlyList<ReferenceDataResponse>>.Ok(
            await areas.ListAsync(new ActiveAreasByCitySpec(query.CityId), cancellationToken));
    }
}
