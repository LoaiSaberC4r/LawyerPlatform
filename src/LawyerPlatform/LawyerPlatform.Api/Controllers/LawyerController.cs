using Asp.Versioning;
using BuildingBlock.Api;
using BuildingBlock.Application.Abstraction.Media;
using LawyerPlatform.Api.Contracts.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.DeleteDocument;
using LawyerPlatform.Application.Features.Lawyers.Dashboard;
using LawyerPlatform.Application.Features.Dashboards;
using LawyerPlatform.Application.Features.Lawyers.GetApprovalStatus;
using LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.GetSettings;
using LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.Update;
using LawyerPlatform.Application.Features.Lawyers.GetOwnDocumentContent;
using LawyerPlatform.Application.Features.Lawyers.GetOwnDocuments;
using LawyerPlatform.Application.Features.Lawyers.GetOwnProfile;
using LawyerPlatform.Application.Features.Lawyers.GetOwnProfileImage;
using LawyerPlatform.Application.Features.Lawyers.ReplaceSpecializations;
using LawyerPlatform.Application.Features.Lawyers.SubmitForApproval;
using LawyerPlatform.Application.Features.Lawyers.UpdateOwnProfile;
using LawyerPlatform.Application.Features.Lawyers.UpdateProfileImage;
using LawyerPlatform.Application.Features.Lawyers.UploadDocument;
using LawyerPlatform.Application.Features.Lawyers.UpsertPrimaryOffice;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LawyerPlatform.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "LawyerOnly")]
[Route("api/v{version:apiVersion}/lawyer")]
public sealed class LawyerController(ISender sender) : ControllerBase
{
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(RequestDashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
        => (await sender.Send(new GetLawyerDashboardQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
        => (await sender.Send(new GetOwnProfileQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("consultation-settings")]
    public async Task<IActionResult> GetConsultationSettings(CancellationToken cancellationToken)
        => (await sender.Send(new GetLawyerConsultationSettingsQuery(), cancellationToken))
            .ToIActionResult(cancellationToken);

    [HttpPut("consultation-settings")]
    public async Task<IActionResult> UpdateConsultationSettings(
        UpdateLawyerConsultationSettingsRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new UpdateLawyerConsultationSettingsCommand(
            request.ConsultationPrice,
            request.Availability?.Select(item => new UpdateLawyerAvailabilityItem(
                item.DayOfWeek,
                item.StartTime,
                item.EndTime)).ToArray(),
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateLawyerProfileRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new UpdateOwnProfileCommand(
            request.FullName,
            request.ProfessionalTitle,
            request.Biography,
            request.YearsOfExperience,
            request.ProfessionalRegistrationNumber,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPut("profile/image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProfileImage(
        [FromForm] UpdateLawyerProfileImageRequest request,
        CancellationToken cancellationToken)
    {
        await using var stream = request.Image.OpenReadStream();
        var result = await sender.Send(new UpdateProfileImageCommand(
            new MediaUpload(stream, request.Image.FileName, request.Image.ContentType, request.Image.Length),
            request.RowVersion), cancellationToken);
        return result.ToIActionResult(cancellationToken);
    }

    [HttpGet("profile/image")]
    public async Task<IActionResult> GetProfileImage(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOwnProfileImageQuery(), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpPut("office")]
    public async Task<IActionResult> UpsertOffice(UpsertLawyerOfficeRequest request, CancellationToken cancellationToken)
        => (await sender.Send(new UpsertPrimaryOfficeCommand(
            request.GovernorateId,
            request.CityId,
            request.AreaId,
            request.DetailedAddress,
            request.PublicPhoneNumber,
            request.Latitude,
            request.Longitude,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPut("specializations")]
    public async Task<IActionResult> ReplaceSpecializations(
        ReplaceLawyerSpecializationsRequest request,
        CancellationToken cancellationToken)
        => (await sender.Send(new ReplaceSpecializationsCommand(
            request.SpecializationIds,
            request.RowVersion), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments(CancellationToken cancellationToken)
        => (await sender.Send(new GetOwnDocumentsQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument(
        [FromForm] UploadLawyerDocumentRequest request,
        CancellationToken cancellationToken)
    {
        await using var stream = request.File.OpenReadStream();
        var result = await sender.Send(new UploadDocumentCommand(
            request.DocumentType,
            new MediaUpload(stream, request.File.FileName, request.File.ContentType, request.File.Length)), cancellationToken);
        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpGet("documents/{documentId:guid}/content")]
    public async Task<IActionResult> GetDocumentContent(Guid documentId, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOwnDocumentContentQuery(documentId), cancellationToken);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName, enableRangeProcessing: true)
            : result.ToIActionResult(cancellationToken);
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(
        Guid documentId,
        [FromHeader(Name = "If-Match")] string rowVersion,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteDocumentCommand(documentId, rowVersion), cancellationToken);
        return result.IsSuccess ? NoContent() : result.ToIActionResult(cancellationToken);
    }

    [HttpGet("approval-status")]
    public async Task<IActionResult> GetApprovalStatus(CancellationToken cancellationToken)
        => (await sender.Send(new GetApprovalStatusQuery(), cancellationToken)).ToIActionResult(cancellationToken);

    [HttpPost("submit-for-approval")]
    public async Task<IActionResult> SubmitForApproval(
        [FromHeader(Name = "If-Match")] string rowVersion,
        CancellationToken cancellationToken)
        => (await sender.Send(new SubmitForApprovalCommand(rowVersion), cancellationToken)).ToIActionResult(cancellationToken);
}
