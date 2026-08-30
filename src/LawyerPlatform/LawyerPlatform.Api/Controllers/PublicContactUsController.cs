using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Api.Contracts.ContactInquiries;
using LawyerPlatform.Application.Features.ContactInquiries.Create;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/public/contact-us")]
public sealed class PublicContactUsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("consultation-guest")]
    [ProducesResponseType(typeof(CreateContactInquiryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Create(
        CreateContactInquiryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateContactInquiryCommand(
                request.FullName,
                request.PhoneNumber,
                request.Email,
                request.InquiryType,
                request.Message),
            cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToIActionResult(cancellationToken);
    }
}
