using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.SharedDto;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ContactInquiries;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Application.Features.ContactInquiries.Admin.GetList;

public sealed record GetContactInquiriesQuery(int PageNumber = 1, int PageSize = 20)
    : IQuery<PagedResult<AdminContactInquiryListItemResponse>>;

public sealed record AdminContactInquiryListItemResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string Email,
    string InquiryType,
    DateTime CreatedOnUtc);

internal sealed class GetContactInquiriesQueryValidator
    : AbstractValidator<GetContactInquiriesQuery>
{
    public GetContactInquiriesQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThan(0)
            .WithErrorCode("ContactInquiry.PageNumberInvalid")
            .WithMessage(_ => ErrorMessage.PaginationPageNumberInvalid);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithErrorCode("ContactInquiry.PageSizeInvalid")
            .WithMessage(_ => ErrorMessage.PaginationPageSizeInvalid);
    }
}

internal sealed class GetContactInquiriesQueryHandler(
    IReadRepository<ContactInquiry, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetContactInquiriesQuery, PagedResult<AdminContactInquiryListItemResponse>>
{
    public async Task<Result<PagedResult<AdminContactInquiryListItemResponse>>> Handle(
        GetContactInquiriesQuery query,
        CancellationToken cancellationToken)
    {
        var (items, totalCount) = await repository.ListWithLongCountAsync(
            new AdminContactInquiriesSpecification(query.PageNumber, query.PageSize),
            cancellationToken);

        return Result<PagedResult<AdminContactInquiryListItemResponse>>.Ok(
            new PagedResult<AdminContactInquiryListItemResponse>(
                query.PageNumber,
                query.PageSize,
                totalCount,
                items));
    }
}

internal sealed class AdminContactInquiriesSpecification
    : Specification<ContactInquiry, AdminContactInquiryListItemResponse>
{
    public AdminContactInquiriesSpecification(int pageNumber, int pageSize)
    {
        AddOrderByDescending(inquiry => inquiry.CreatedOnUtc);
        AddOrderByDescending(inquiry => inquiry.Id);
        ApplyPaging(pageNumber, pageSize, 100);
        UseNoTracking();
        Select(inquiry => new AdminContactInquiryListItemResponse(
            inquiry.Id,
            inquiry.FullName,
            inquiry.PhoneNumber,
            inquiry.Email,
            inquiry.InquiryType,
            inquiry.CreatedOnUtc));
    }
}
