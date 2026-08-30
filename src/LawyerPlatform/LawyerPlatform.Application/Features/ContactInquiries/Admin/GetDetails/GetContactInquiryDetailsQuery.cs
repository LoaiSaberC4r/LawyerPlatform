using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ContactInquiries;

namespace LawyerPlatform.Application.Features.ContactInquiries.Admin.GetDetails;

public sealed record GetContactInquiryDetailsQuery(Guid Id)
    : IQuery<AdminContactInquiryDetailsResponse>;

public sealed record AdminContactInquiryDetailsResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string Email,
    string InquiryType,
    string Message,
    DateTime CreatedOnUtc);

internal sealed class GetContactInquiryDetailsQueryHandler(
    IReadRepository<ContactInquiry, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetContactInquiryDetailsQuery, AdminContactInquiryDetailsResponse>
{
    public async Task<Result<AdminContactInquiryDetailsResponse>> Handle(
        GetContactInquiryDetailsQuery query,
        CancellationToken cancellationToken)
    {
        var response = await repository.FirstOrDefaultAsync(
            new AdminContactInquiryDetailsSpecification(query.Id),
            cancellationToken);

        return response is null
            ? Result<AdminContactInquiryDetailsResponse>.Fail(ContactInquiryErrors.NotFound)
            : Result<AdminContactInquiryDetailsResponse>.Ok(response);
    }
}

internal sealed class AdminContactInquiryDetailsSpecification
    : Specification<ContactInquiry, AdminContactInquiryDetailsResponse>
{
    public AdminContactInquiryDetailsSpecification(Guid id)
    {
        AddCriteria(inquiry => inquiry.Id == id);
        UseNoTracking();
        Select(inquiry => new AdminContactInquiryDetailsResponse(
            inquiry.Id,
            inquiry.FullName,
            inquiry.PhoneNumber,
            inquiry.Email,
            inquiry.InquiryType,
            inquiry.Message,
            inquiry.CreatedOnUtc));
    }
}
