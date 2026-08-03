using Asp.Versioning;
using BuildingBlock.Api;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LawyerPlatform.Api.Contracts.Catalog;
using LawyerPlatform.Application.Catalog.Create;
using LawyerPlatform.Application.Catalog.Delete;
using LawyerPlatform.Application.Catalog.GetById;
using LawyerPlatform.Application.Catalog.GetPage;
using LawyerPlatform.Application.Catalog.PermanentDelete;
using LawyerPlatform.Application.Catalog.Restore;
using LawyerPlatform.Application.Catalog.Update;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/catalog-items")]
public sealed class CatalogItemsController(ISender sender) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPage(
        [FromQuery] GetCatalogItemsPageQuery query,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(query, cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetCatalogItemByIdQuery(id), cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(
        [FromBody] CreateCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateCatalogItemCommand(request.Name, request.Description, request.Price),
            cancellationToken);

        return result.ToIActionResult(cancellationToken);
    }

    [HttpPut("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateCatalogItemCommand(id, request.Name, request.Description, request.Price),
            cancellationToken);

        return result.ToIActionResult(cancellationToken);
    }

    [HttpDelete("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteCatalogItemCommand(id), cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }

    [HttpPost("{id:guid}/restore")]
    [AllowAnonymous]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RestoreCatalogItemCommand(id), cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }

    [HttpDelete("{id:guid}/permanent")]
    [Authorize]
    public async Task<IActionResult> PermanentDelete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PermanentDeleteCatalogItemCommand(id), cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }
}
