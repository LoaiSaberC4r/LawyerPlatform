using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Domain.SharedDto;
using LawyerPlatform.Api.Contracts.Clients;
using LawyerPlatform.Api.OpenApi;
using LawyerPlatform.Application.Features.AdminClients.Common;
using LawyerPlatform.Application.Features.AdminClients.GetClient;
using LawyerPlatform.Application.Features.AdminClients.GetClients;
using LawyerPlatform.Application.Features.AdminClients.ReactivateClient;
using LawyerPlatform.Application.Features.AdminClients.SuspendClient;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "SuperAdminOnly")]
[Route("api/v{version:apiVersion}/admin/clients")]
public sealed class AdminClientsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AdminClientListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClients(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(
            new GetAdminClientsQuery(pageNumber, pageSize),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{clientId:guid}")]
    [ProducesResponseType(typeof(AdminClientDetailsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClient(
        [FromRoute, OpenApiParameterDescription("ClientProfile.Id (not UserAccount.Id).")] Guid clientId,
        CancellationToken cancellationToken)
        => (await sender.Send(new GetAdminClientQuery(clientId), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{clientId:guid}/suspend")]
    [ProducesResponseType(typeof(AdminClientLifecycleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Suspend(
        [FromRoute, OpenApiParameterDescription("ClientProfile.Id (not UserAccount.Id).")] Guid clientId,
        ChangeClientAccountStatusRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new SuspendClientCommand(clientId, request.RowVersion),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{clientId:guid}/reactivate")]
    [ProducesResponseType(typeof(AdminClientLifecycleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reactivate(
        [FromRoute, OpenApiParameterDescription("ClientProfile.Id (not UserAccount.Id).")] Guid clientId,
        ChangeClientAccountStatusRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new ReactivateClientCommand(clientId, request.RowVersion),
            cancellationToken)).ToIActionResult(cancellationToken);
}
