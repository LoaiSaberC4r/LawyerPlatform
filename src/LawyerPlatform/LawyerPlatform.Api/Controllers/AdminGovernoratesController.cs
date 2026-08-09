using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Api.Contracts.ReferenceData;
using LawyerPlatform.Application.Features.AdminLocations.Governorates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController, ApiVersion("1.0"), Authorize(Policy = "SuperAdminOnly")]
[Route("api/v{version:apiVersion}/admin/governorates")]
public sealed class AdminGovernoratesController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchText, [FromQuery] bool? isActive,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new GetAdminGovernoratesQuery(
            searchText, isActive, pageNumber, pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        => (await sender.Send(new GetAdminGovernorateQuery(id), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Create(CreateGovernorateRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new CreateGovernorateCommand(
            request.NameAr, request.NameEn, request.DisplayOrder), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id, version = "1" }, result.Value)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateGovernorateRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new UpdateGovernorateCommand(
            id, request.NameAr, request.NameEn, request.DisplayOrder, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, ChangeReferenceDataStatusRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new ChangeGovernorateStatusCommand(id, true, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, ChangeReferenceDataStatusRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new ChangeGovernorateStatusCommand(id, false, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);
}
