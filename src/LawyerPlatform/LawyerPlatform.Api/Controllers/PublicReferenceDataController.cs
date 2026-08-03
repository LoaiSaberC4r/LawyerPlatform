using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Application.Features.ReferenceData.GetAreas;
using LawyerPlatform.Application.Features.ReferenceData.GetCities;
using LawyerPlatform.Application.Features.ReferenceData.GetGovernorates;
using LawyerPlatform.Application.Features.ReferenceData.GetLegalSpecializations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/public")]
public sealed class PublicReferenceDataController(ISender sender) : ControllerBase
{
    [HttpGet("governorates")]
    public async Task<IActionResult> GetGovernorates(CancellationToken cancellationToken)
        => (await sender.Send(new GetGovernoratesQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("governorates/{governorateId:int}/cities")]
    public async Task<IActionResult> GetCities(int governorateId, CancellationToken cancellationToken)
        => (await sender.Send(new GetCitiesQuery(governorateId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("cities/{cityId:int}/areas")]
    public async Task<IActionResult> GetAreas(int cityId, CancellationToken cancellationToken)
        => (await sender.Send(new GetAreasQuery(cityId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("legal-specializations")]
    public async Task<IActionResult> GetLegalSpecializations(CancellationToken cancellationToken)
        => (await sender.Send(new GetLegalSpecializationsQuery(), cancellationToken)).ToIActionResult(cancellationToken);
}
