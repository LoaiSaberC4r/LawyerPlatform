using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.PublicLawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.PublicLawyers.SearchLawyers;

public sealed record SearchLawyersQuery(
    string? SearchText,
    int? GovernorateId,
    int? CityId,
    int? AreaId,
    int? SpecializationId,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedResult<PublicLawyerResponse>>;

internal sealed class SearchLawyersQueryValidator : AbstractValidator<SearchLawyersQuery>
{
    public SearchLawyersQueryValidator()
    {
        RuleFor(query => query.SearchText).MaximumLength(200);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.GovernorateId).GreaterThan(0).When(query => query.GovernorateId.HasValue);
        RuleFor(query => query.CityId).GreaterThan(0).When(query => query.CityId.HasValue);
        RuleFor(query => query.AreaId).GreaterThan(0).When(query => query.AreaId.HasValue);
        RuleFor(query => query.SpecializationId).GreaterThan(0).When(query => query.SpecializationId.HasValue);
    }
}

internal sealed class SearchLawyersQueryHandler(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    ILawyerDocumentPolicy documentPolicy)
    : IQueryHandler<SearchLawyersQuery, PagedResult<PublicLawyerResponse>>
{
    public async Task<Result<PagedResult<PublicLawyerResponse>>> Handle(SearchLawyersQuery query, CancellationToken cancellationToken)
    {
        var (items, count) = await repository.ListWithLongCountAsync(
            new PublicLawyersSearchSpecification(query, documentPolicy),
            cancellationToken);
        return Result<PagedResult<PublicLawyerResponse>>.Ok(new PagedResult<PublicLawyerResponse>(
            query.PageNumber,
            query.PageSize,
            count,
            items.Select(item => item.ToResponse())));
    }
}

internal sealed class PublicLawyersSearchSpecification : PublicLawyerSpecification<PublicLawyerSnapshot>
{
    public PublicLawyersSearchSpecification(SearchLawyersQuery query, ILawyerDocumentPolicy documentPolicy)
    {
        ApplyPublicEligibility(documentPolicy);
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            AddCriteria(profile =>
                profile.FullName.Contains(searchText) ||
                (profile.ProfessionalTitle != null && profile.ProfessionalTitle.Contains(searchText)) ||
                (profile.Biography != null && profile.Biography.Contains(searchText)));
        }

        if (query.GovernorateId.HasValue)
        {
            AddCriteria(profile => profile.Offices.Any(office =>
                office.IsPrimary && office.IsActive && office.GovernorateId == query.GovernorateId.Value));
        }

        if (query.CityId.HasValue)
        {
            AddCriteria(profile => profile.Offices.Any(office =>
                office.IsPrimary && office.IsActive && office.CityId == query.CityId.Value));
        }

        if (query.AreaId.HasValue)
        {
            AddCriteria(profile => profile.Offices.Any(office =>
                office.IsPrimary && office.IsActive && office.AreaId == query.AreaId.Value));
        }

        if (query.SpecializationId.HasValue)
        {
            AddCriteria(profile => profile.Specializations.Any(item =>
                item.LegalSpecializationId == query.SpecializationId.Value));
        }

        AddOrderBy(profile => profile.FullName);
        AddOrderBy(profile => profile.Id);
        ApplyPaging(query.PageNumber, query.PageSize, 100);
        UseNoTracking();
        UseSplitQuery();
        Select(PublicLawyerProjection.Create());
    }
}
