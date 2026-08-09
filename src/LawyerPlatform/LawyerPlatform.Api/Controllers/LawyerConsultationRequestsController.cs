using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Api.Contracts.Consultations;
using LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.GetRequest;
using LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.GetRequests;
using LawyerPlatform.Application.Features.ConsultationRequests.Lawyer.UpdateStatus;
using LawyerPlatform.Domain.Consultations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "LawyerOnly")]
[Route("api/v{version:apiVersion}/lawyer/consultation-requests")]
public sealed class LawyerConsultationRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] ConsultationRequestStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new GetLawyerConsultationRequestsQuery(
            status,
            pageNumber,
            pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> GetById(Guid requestId, CancellationToken cancellationToken)
        => (await sender.Send(new GetLawyerConsultationRequestQuery(requestId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPut("{requestId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid requestId,
        UpdateConsultationStatusRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateLawyerConsultationStatusCommand(
            requestId,
            request.Status,
            request.Reason,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);
}
