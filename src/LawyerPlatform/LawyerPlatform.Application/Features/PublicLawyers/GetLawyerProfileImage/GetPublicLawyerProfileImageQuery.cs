using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Features.PublicLawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.PublicLawyers.GetLawyerProfileImage;

public sealed record GetPublicLawyerProfileImageQuery(Guid LawyerId) : IQuery<StoredFileResponse>;

internal sealed class GetPublicLawyerProfileImageQueryHandler(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    ILawyerDocumentPolicy documentPolicy,
    IStoredFileReader storedFileReader)
    : IQueryHandler<GetPublicLawyerProfileImageQuery, StoredFileResponse>
{
    public async Task<Result<StoredFileResponse>> Handle(GetPublicLawyerProfileImageQuery query, CancellationToken cancellationToken)
    {
        var image = await repository.FirstOrDefaultAsync(
            new PublicLawyerImageSpecification(query.LawyerId, documentPolicy),
            cancellationToken);
        if (image?.StorageKey is null)
        {
            return Result<StoredFileResponse>.Fail(LawyerErrors.NotFound);
        }

        var stored = await storedFileReader.OpenReadAsync(
            image.StorageKey,
            image.FileName,
            "application/octet-stream",
            cancellationToken);
        return stored is null
            ? Result<StoredFileResponse>.Fail(LawyerErrors.NotFound)
            : Result<StoredFileResponse>.Ok(new StoredFileResponse(stored.Content, stored.ContentType, stored.FileName, stored.Length));
    }
}

internal sealed class PublicLawyerImageSpecification : PublicLawyerSpecification<PublicLawyerImageSnapshot>
{
    public PublicLawyerImageSpecification(Guid lawyerId, ILawyerDocumentPolicy documentPolicy)
    {
        AddCriteria(profile => profile.Id == lawyerId && profile.ProfileImageStorageKey != null);
        ApplyPublicEligibility(documentPolicy);
        UseNoTracking();
        Select(profile => new PublicLawyerImageSnapshot(
            profile.ProfileImageStorageKey,
            $"lawyer-{profile.Id}-profile-image"));
    }
}

internal sealed record PublicLawyerImageSnapshot(string? StorageKey, string FileName);
