using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Api.Contracts.Consultations;
using LawyerPlatform.Application.Features.ConsultationRequests.CreateGuest;
using LawyerPlatform.Application.Features.ConsultationRequests.Guest.TrackRequest;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/public/consultation-requests")]
public sealed class PublicConsultationRequestsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("consultation-guest")]
    public async Task<IActionResult> Create(
        CreateGuestConsultationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateGuestConsultationRequestCommand(
            request.LawyerId,
            request.LegalSpecializationId,
            request.FullName,
            request.PhoneNumber,
            request.Email,
            request.Description,
            request.PreferredAppointmentOnUtc), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpPost("track")]
    [EnableRateLimiting("consultation-track")]
    public async Task<IActionResult> Track(
        GuestTrackingRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new TrackGuestConsultationRequestQuery(
            request.ReferenceNumber,
            request.PhoneNumber), cancellationToken)).ToIActionResult(cancellationToken);
}
