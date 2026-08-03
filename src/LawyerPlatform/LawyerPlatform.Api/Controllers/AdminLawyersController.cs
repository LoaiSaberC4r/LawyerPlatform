using Asp.Versioning;
using BuildingBlock.Api;
using LawyerPlatform.Api.Contracts.Lawyers;
using LawyerPlatform.Application.Features.AdminLawyers.ApproveLawyer;
using LawyerPlatform.Application.Features.AdminLawyers.GetLawyerDetails;
using LawyerPlatform.Application.Features.AdminLawyers.GetLawyerDocumentContent;
using LawyerPlatform.Application.Features.AdminLawyers.GetLawyerProfileImage;
using LawyerPlatform.Application.Features.AdminLawyers.GetLawyers;
using LawyerPlatform.Application.Features.AdminLawyers.ReactivateLawyer;
using LawyerPlatform.Application.Features.AdminLawyers.RejectLawyer;
using LawyerPlatform.Application.Features.AdminLawyers.RequestLawyerChanges;
using LawyerPlatform.Application.Features.AdminLawyers.SuspendLawyer;
using LawyerPlatform.Domain.Lawyers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "SuperAdminOnly")]
[Route("api/v{version:apiVersion}/admin/lawyers")]
public sealed class AdminLawyersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetLawyers(
        [FromQuery] string? searchText,
        [FromQuery] LawyerApprovalStatus? approvalStatus,
        [FromQuery] int? governorateId,
        [FromQuery] int? specializationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => (await sender.Send(new GetLawyersQuery(
            searchText,
            approvalStatus,
            governorateId,
            specializationId,
            pageNumber,
            pageSize), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{lawyerId:guid}")]
    public async Task<IActionResult> GetLawyer(Guid lawyerId, CancellationToken cancellationToken)
        => (await sender.Send(new GetLawyerDetailsQuery(lawyerId), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("{lawyerId:guid}/profile-image")]
    public async Task<IActionResult> GetProfileImage(Guid lawyerId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetLawyerProfileImageQuery(lawyerId), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpGet("{lawyerId:guid}/documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(
        Guid lawyerId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetLawyerDocumentContentQuery(lawyerId, documentId), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpPost("{lawyerId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid lawyerId,
        LawyerDecisionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new ApproveLawyerCommand(lawyerId, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{lawyerId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid lawyerId,
        RejectLawyerRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new RejectLawyerCommand(lawyerId, request.Reason, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{lawyerId:guid}/request-changes")]
    public async Task<IActionResult> RequestChanges(
        Guid lawyerId,
        RequestLawyerChangesRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new RequestLawyerChangesCommand(
            lawyerId,
            request.Explanation,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("{lawyerId:guid}/suspend")]
    public async Task<IActionResult> Suspend(
        Guid lawyerId,
        SuspendLawyerRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new SuspendLawyerCommand(lawyerId, request.Reason, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPost("{lawyerId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(
        Guid lawyerId,
        LawyerDecisionRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new ReactivateLawyerCommand(lawyerId, request.RowVersion), cancellationToken))
            .ToIActionResult(cancellationToken);
}
