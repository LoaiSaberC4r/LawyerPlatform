using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Application.Features.PublicLawyers.GetLawyerDetails;
using LawyerPlatform.Application.Features.PublicLawyers.GetConsultationSettings;
using LawyerPlatform.Application.Features.PublicLawyers.GetLawyerProfileImage;
using LawyerPlatform.Application.Features.PublicLawyers.SearchLawyers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/public/lawyers")]
public sealed class PublicLawyersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? searchText,
        [FromQuery] int? governorateId,
        [FromQuery] int? cityId,
        [FromQuery] int? areaId,
        [FromQuery] int? specializationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new SearchLawyersQuery(
            searchText,
            governorateId,
            cityId,
            areaId,
            specializationId,
            pageNumber,
            pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{lawyerId:guid}")]
    public async Task<IActionResult> GetById(Guid lawyerId, CancellationToken cancellationToken)
        => (await sender.Send(new GetPublicLawyerDetailsQuery(lawyerId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{lawyerId:guid}/consultation-settings")]
    public async Task<IActionResult> GetConsultationSettings(
        Guid lawyerId,
        CancellationToken cancellationToken)
        => (await sender.Send(
            new GetPublicLawyerConsultationSettingsQuery(lawyerId),
            cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{lawyerId:guid}/profile-image")]
    public async Task<IActionResult> GetProfileImage(Guid lawyerId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPublicLawyerProfileImageQuery(lawyerId), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.ToIActionResult(cancellationToken);
    }
}
