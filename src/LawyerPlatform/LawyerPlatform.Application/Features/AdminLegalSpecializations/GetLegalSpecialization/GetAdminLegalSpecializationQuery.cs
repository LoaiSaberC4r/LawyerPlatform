using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Features.AdminLegalSpecializations.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.AdminLegalSpecializations.GetLegalSpecialization;

public sealed record GetAdminLegalSpecializationQuery(int Id)
    : IQuery<AdminLegalSpecializationResponse>;

internal sealed class GetAdminLegalSpecializationQueryHandler(
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminLegalSpecializationQuery, AdminLegalSpecializationResponse>
{
    public async Task<Result<AdminLegalSpecializationResponse>> Handle(
        GetAdminLegalSpecializationQuery query,
        CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(
            new LegalSpecializationByIdSpecification(query.Id),
            cancellationToken);
        return item is null
            ? Result<AdminLegalSpecializationResponse>.Fail(LegalSpecializationErrors.NotFound)
            : Result<AdminLegalSpecializationResponse>.Ok(item.ToResponse());
    }
}
