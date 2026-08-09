using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.AdminLawyers.GetLawyers;

public sealed record GetLawyersQuery(
    string? SearchText,
    LawyerApprovalStatus? ApprovalStatus,
    int? GovernorateId,
    int? SpecializationId,
    int PageNumber = 1,
    int PageSize = 20)
    : IQuery<PagedResult<AdminLawyerListItemResponse>>;

public sealed record AdminLawyerListItemResponse(
    Guid Id,
    string FullName,
    string? ProfessionalTitle,
    string ApprovalStatus,
    string AccountStatus,
    DateTime? SubmittedOnUtc,
    DateTime CreatedOnUtc,
    int? GovernorateId,
    string? GovernorateNameAr,
    string? GovernorateNameEn,
    IReadOnlyList<LawyerSpecializationResponse> Specializations,
    LawyerProfileCompletionResponse Completion);

internal sealed class GetLawyersQueryValidator : AbstractValidator<GetLawyersQuery>
{
    public GetLawyersQueryValidator()
    {
        RuleFor(query => query.SearchText).MaximumLength(200);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.GovernorateId).GreaterThan(0).When(query => query.GovernorateId.HasValue);
        RuleFor(query => query.SpecializationId).GreaterThan(0).When(query => query.SpecializationId.HasValue);
    }
}

internal sealed class GetLawyersQueryHandler(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    ILawyerDocumentPolicy documentPolicy)
    : IQueryHandler<GetLawyersQuery, PagedResult<AdminLawyerListItemResponse>>
{
    public async Task<Result<PagedResult<AdminLawyerListItemResponse>>> Handle(GetLawyersQuery query, CancellationToken cancellationToken)
    {
        var (items, count) = await repository.ListWithLongCountAsync(new AdminLawyersSpecification(query), cancellationToken);
        var responses = items.Select(item =>
        {
            var completion = LawyerProfileCompletionCalculator.Calculate(
                item.ApprovalStatus,
                item.FullName,
                item.ProfessionalTitle,
                item.YearsOfExperience,
                item.ProfessionalRegistrationNumber,
                item.OfficeComplete,
                item.Specializations.Any(specialization => specialization.IsActive),
                item.ActiveDocumentTypes,
                item.AccountStatus == AccountStatus.Active,
                documentPolicy);
            return new AdminLawyerListItemResponse(
                item.Id,
                item.FullName,
                item.ProfessionalTitle,
                item.ApprovalStatus.ToString(),
                item.AccountStatus.ToString(),
                item.SubmittedOnUtc,
                item.CreatedOnUtc,
                item.Office?.GovernorateId,
                item.Office?.GovernorateNameAr,
                item.Office?.GovernorateNameEn,
                item.Specializations.Select(specialization => new LawyerSpecializationResponse(
                    specialization.Id,
                    specialization.NameAr,
                    specialization.NameEn)).ToArray(),
                completion);
        }).ToArray();

        return Result<PagedResult<AdminLawyerListItemResponse>>.Ok(
            new PagedResult<AdminLawyerListItemResponse>(query.PageNumber, query.PageSize, count, responses));
    }
}

internal sealed class AdminLawyersSpecification : Specification<LawyerProfile, AdminLawyerListSnapshot>
{
    public AdminLawyersSpecification(GetLawyersQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText.Trim();
            AddCriteria(profile =>
                profile.FullName.Contains(searchText) ||
                (profile.ProfessionalTitle != null && profile.ProfessionalTitle.Contains(searchText)) ||
                profile.UserAccount.UserName.Contains(searchText));
        }

        if (query.ApprovalStatus.HasValue)
        {
            AddCriteria(profile => profile.ApprovalStatus == query.ApprovalStatus.Value);
        }

        if (query.GovernorateId.HasValue)
        {
            AddCriteria(profile => profile.Offices.Any(office =>
                office.IsPrimary && office.IsActive && office.GovernorateId == query.GovernorateId.Value));
        }

        if (query.SpecializationId.HasValue)
        {
            AddCriteria(profile => profile.Specializations.Any(item =>
                item.LegalSpecializationId == query.SpecializationId.Value));
        }

        AddOrderByDescending(profile => profile.SubmittedOnUtc);
        AddOrderByDescending(profile => profile.CreatedOnUtc);
        AddOrderBy(profile => profile.Id);
        ApplyPaging(query.PageNumber, query.PageSize, 100);
        UseNoTracking();
        UseSplitQuery();
        Select(profile => new AdminLawyerListSnapshot(
            profile.Id,
            profile.FullName,
            profile.ProfessionalTitle,
            profile.YearsOfExperience,
            profile.ProfessionalRegistrationNumber,
            profile.ApprovalStatus,
            profile.UserAccount.Status,
            profile.SubmittedOnUtc,
            profile.CreatedOnUtc,
            profile.Offices.Where(office => office.IsPrimary && office.IsActive)
                .Select(office => new AdminOfficeSummarySnapshot(
                    office.GovernorateId,
                    office.Governorate.NameAr,
                    office.Governorate.NameEn))
                .FirstOrDefault(),
            profile.Offices.Any(office =>
                office.IsPrimary && office.IsActive && office.Governorate.IsActive && office.City.IsActive && office.Area.IsActive &&
                office.City.GovernorateId == office.GovernorateId && office.Area.CityId == office.CityId && office.DetailedAddress != ""),
            profile.Specializations.OrderBy(item => item.LegalSpecialization.DisplayOrder)
                .Select(item => new AdminSpecializationSnapshot(
                    item.LegalSpecializationId,
                    item.LegalSpecialization.NameAr,
                    item.LegalSpecialization.NameEn,
                    item.LegalSpecialization.IsActive)).ToArray(),
            profile.Documents.Where(document => !document.IsDeleted).Select(document => document.DocumentType).ToArray()));
    }
}

internal sealed record AdminLawyerListSnapshot(
    Guid Id,
    string FullName,
    string? ProfessionalTitle,
    int? YearsOfExperience,
    string? ProfessionalRegistrationNumber,
    LawyerApprovalStatus ApprovalStatus,
    AccountStatus AccountStatus,
    DateTime? SubmittedOnUtc,
    DateTime CreatedOnUtc,
    AdminOfficeSummarySnapshot? Office,
    bool OfficeComplete,
    IReadOnlyList<AdminSpecializationSnapshot> Specializations,
    IReadOnlyList<string> ActiveDocumentTypes);

internal sealed record AdminOfficeSummarySnapshot(int GovernorateId, string GovernorateNameAr, string GovernorateNameEn);
internal sealed record AdminSpecializationSnapshot(int Id, string NameAr, string NameEn, bool IsActive);
