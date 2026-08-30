using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Domain.SharedDto;
using LawyerPlatform.Application.Features.ContactInquiries.Admin.GetDetails;
using LawyerPlatform.Application.Features.ContactInquiries.Admin.GetList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "SuperAdminOnly")]
[Route("api/v{version:apiVersion}/admin/contact-inquiries")]
public sealed class AdminContactInquiriesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(
        typeof(PagedResult<AdminContactInquiryListItemResponse>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(
            new GetContactInquiriesQuery(pageNumber, pageSize),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminContactInquiryDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new GetContactInquiryDetailsQuery(id),
            cancellationToken)).ToIActionResult(cancellationToken);
}
