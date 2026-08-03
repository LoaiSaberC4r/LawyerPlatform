using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Application.Abstractions.Lawyers;

public interface ILawyerDocumentPolicy
{
    IReadOnlyList<string> RequiredDocumentTypes { get; }

    Task<Result> ValidateAsync(
        string documentType,
        MediaUpload upload,
        CancellationToken cancellationToken = default);
}
