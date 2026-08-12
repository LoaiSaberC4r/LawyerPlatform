using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.PublicLawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.PublicLawyers.GetLawyerDetails;

public sealed record GetPublicLawyerDetailsQuery(Guid LawyerId) : IQuery<PublicLawyerResponse>;

internal sealed class GetPublicLawyerDetailsQueryHandler(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    ILawyerDocumentPolicy documentPolicy)
    : IQueryHandler<GetPublicLawyerDetailsQuery, PublicLawyerResponse>
{
    public async Task<Result<PublicLawyerResponse>> Handle(GetPublicLawyerDetailsQuery query, CancellationToken cancellationToken)
    {
        var item = await repository.FirstOrDefaultAsync(
            new PublicLawyerDetailsSpecification(query.LawyerId, documentPolicy),
            cancellationToken);
        return item is null
            ? Result<PublicLawyerResponse>.Fail(LawyerErrors.NotFound)
            : Result<PublicLawyerResponse>.Ok(item.ToResponse());
    }
}

internal sealed class PublicLawyerDetailsSpecification : PublicLawyerSpecification<PublicLawyerDetailsSnapshot>
{
    public PublicLawyerDetailsSpecification(Guid lawyerId, ILawyerDocumentPolicy documentPolicy)
    {
        AddCriteria(profile => profile.Id == lawyerId);
        ApplyPublicEligibility(documentPolicy);
        UseNoTracking();
        UseSplitQuery();
        Select(PublicLawyerProjection.CreateDetails());
    }
}
