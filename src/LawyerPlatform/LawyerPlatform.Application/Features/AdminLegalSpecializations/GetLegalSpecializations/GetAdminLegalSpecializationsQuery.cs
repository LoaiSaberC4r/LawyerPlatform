using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Features.AdminLegalSpecializations.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Application.Features.AdminLegalSpecializations.GetLegalSpecializations;

public sealed record GetAdminLegalSpecializationsQuery(
    string? SearchText,
    bool? IsActive,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedResult<AdminLegalSpecializationResponse>>;

internal sealed class GetAdminLegalSpecializationsQueryValidator
    : AbstractValidator<GetAdminLegalSpecializationsQuery>
{
    public GetAdminLegalSpecializationsQueryValidator()
    {
        RuleFor(query => query.SearchText).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

internal sealed class GetAdminLegalSpecializationsQueryHandler(
    IReadRepository<LegalSpecialization, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetAdminLegalSpecializationsQuery, PagedResult<AdminLegalSpecializationResponse>>
{
    public async Task<Result<PagedResult<AdminLegalSpecializationResponse>>> Handle(
        GetAdminLegalSpecializationsQuery query,
        CancellationToken cancellationToken)
    {
        var (items, count) = await repository.ListWithLongCountAsync(
            new AdminLegalSpecializationsSpecification(query),
            cancellationToken);
        return Result<PagedResult<AdminLegalSpecializationResponse>>.Ok(
            new PagedResult<AdminLegalSpecializationResponse>(
                query.PageNumber,
                query.PageSize,
                count,
                items.Select(item => item.ToResponse())));
    }
}

internal sealed class AdminLegalSpecializationsSpecification
    : Specification<LegalSpecialization, AdminLegalSpecializationSnapshot>
{
    public AdminLegalSpecializationsSpecification(GetAdminLegalSpecializationsQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            AddCriteria(item => item.NameAr.Contains(searchText) || item.NameEn.Contains(searchText));
        }

        if (query.IsActive.HasValue)
        {
            AddCriteria(item => item.IsActive == query.IsActive.Value);
        }

        AddOrderBy(item => item.DisplayOrder);
        AddOrderBy(item => item.Id);
        ApplyPaging(query.PageNumber, query.PageSize, 100);
        UseNoTracking();
        Select(item => new AdminLegalSpecializationSnapshot(
            item.Id,
            item.NameAr,
            item.NameEn,
            item.DisplayOrder,
            item.IsActive,
            item.RowVersion));
    }
}
