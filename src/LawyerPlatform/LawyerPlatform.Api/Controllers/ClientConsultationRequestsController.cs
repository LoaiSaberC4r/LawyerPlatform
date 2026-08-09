using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Api.Contracts.Consultations;
using LawyerPlatform.Application.Features.ConsultationRequests.CreateClient;
using LawyerPlatform.Application.Features.ConsultationRequests.Client.GetRequest;
using LawyerPlatform.Application.Features.ConsultationRequests.Client.GetRequests;
using LawyerPlatform.Domain.Consultations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "ClientOnly")]
[Route("api/v{version:apiVersion}/client/consultation-requests")]
public sealed class ClientConsultationRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] ConsultationRequestStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new GetClientConsultationRequestsQuery(
            status,
            pageNumber,
            pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> GetById(Guid requestId, CancellationToken cancellationToken)
        => (await sender.Send(new GetClientConsultationRequestQuery(requestId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateClientConsultationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateClientConsultationRequestCommand(
            request.LawyerId,
            request.LegalSpecializationId,
            request.Description,
            request.PreferredAppointmentOnUtc), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToIActionResult(cancellationToken);
    }
}
