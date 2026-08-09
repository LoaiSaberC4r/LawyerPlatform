using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Api.Contracts.Consultations;
using LawyerPlatform.Application.Features.ConsultationRequests.Admin.GetRequest;
using LawyerPlatform.Application.Features.ConsultationRequests.Admin.GetRequests;
using LawyerPlatform.Application.Features.ConsultationRequests.Admin.UpdateStatus;
using LawyerPlatform.Domain.Consultations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "SuperAdminOnly")]
[Route("api/v{version:apiVersion}/admin/consultation-requests")]
public sealed class AdminConsultationRequestsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchText,
        [FromQuery] ConsultationRequestStatus? status,
        [FromQuery] Guid? lawyerId,
        [FromQuery] Guid? clientProfileId,
        [FromQuery] string? requesterType,
        [FromQuery] DateTime? createdFromUtc,
        [FromQuery] DateTime? createdToUtc,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new GetAdminConsultationRequestsQuery(
            searchText, status, lawyerId, clientProfileId, requesterType,
            createdFromUtc, createdToUtc, pageNumber, pageSize), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> GetById(Guid requestId, CancellationToken cancellationToken)
        => (await sender.Send(new GetAdminConsultationRequestQuery(requestId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPut("{requestId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid requestId,
        UpdateConsultationStatusRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateAdminConsultationStatusCommand(
            requestId, request.Status, request.Reason, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);
}
