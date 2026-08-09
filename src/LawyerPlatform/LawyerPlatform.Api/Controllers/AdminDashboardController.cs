using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Application.Features.AdminDashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "SuperAdminOnly")]
[Route("api/v{version:apiVersion}/admin/dashboard")]
public sealed class AdminDashboardController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AdminDashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => (await sender.Send(new GetAdminDashboardQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);
}
