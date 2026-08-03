using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace LawyerPlatform.Infrastructure.Lawyers;

internal sealed class LawyerDocumentPolicy(
    IOptions<LawyerDocumentOptions> options,
    IMediaUploadValidator uploadValidator)
    : ILawyerDocumentPolicy
{
    private readonly LawyerDocumentOptions _options = options.Value;

    public IReadOnlyList<string> RequiredDocumentTypes => _options.RequiredDocumentTypes;

    public async Task<Result> ValidateAsync(
        string documentType,
        MediaUpload upload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentType))
        {
            return Result.Fail(LawyerErrors.DocumentTypeRequired);
        }

        if (!_options.RequiredDocumentTypes.Contains(documentType.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return Result.Fail(LawyerErrors.InvalidDocumentType);
        }

        if (upload is null || upload.Content is null || string.IsNullOrWhiteSpace(upload.FileName))
        {
            return Result.Fail(LawyerErrors.InvalidDocument);
        }

        var fileName = upload.FileName;
        var extension = Path.GetExtension(fileName);
        if (Path.IsPathRooted(fileName) || fileName != Path.GetFileName(fileName) ||
            fileName.Contains(':', StringComparison.Ordinal) ||
            fileName.Contains('/', StringComparison.Ordinal) || fileName.Contains('\\', StringComparison.Ordinal) ||
            !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(upload.ContentType) ||
            !_options.AllowedContentTypes.Contains(upload.ContentType, StringComparer.OrdinalIgnoreCase) ||
            upload.Length is <= 0 || upload.Length > _options.MaximumFileSizeBytes)
        {
            return Result.Fail(LawyerErrors.InvalidDocument);
        }

        try
        {
            await using var validated = await uploadValidator.ValidateAsync(upload, cancellationToken);
            if (validated.Length > _options.MaximumFileSizeBytes ||
                !_options.AllowedExtensions.Contains(validated.Extension, StringComparer.OrdinalIgnoreCase) ||
                !_options.AllowedContentTypes.Contains(validated.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return Result.Fail(LawyerErrors.InvalidDocument);
            }

            return Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return Result.Fail(LawyerErrors.InvalidDocument);
        }
    }
}
