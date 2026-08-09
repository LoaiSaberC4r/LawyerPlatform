using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Api.Contracts.Clients;
using LawyerPlatform.Application.Features.Clients.Dashboard;
using LawyerPlatform.Application.Features.Clients.Profile;
using LawyerPlatform.Application.Features.Dashboards;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "ClientOnly")]
[Route("api/v{version:apiVersion}/client")]
public sealed class ClientController(ISender sender) : ControllerBase
{
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ClientOwnProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
        => (await sender.Send(new GetOwnClientProfileQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPut("profile")]
    [ProducesResponseType(typeof(ClientOwnProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile(
        UpdateClientProfileRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new UpdateOwnClientProfileCommand(request.FullName, request.RowVersion),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(RequestDashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        => (await sender.Send(new GetClientDashboardQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);
}
