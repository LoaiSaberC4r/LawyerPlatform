using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.GetOwnProfileImage;

public sealed record GetOwnProfileImageQuery : IQuery<StoredFileResponse>;

internal sealed class GetOwnProfileImageQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    IStoredFileReader storedFileReader)
    : IQueryHandler<GetOwnProfileImageQuery, StoredFileResponse>
{
    public async Task<Result<StoredFileResponse>> Handle(GetOwnProfileImageQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<StoredFileResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var image = await repository.FirstOrDefaultAsync(new OwnProfileImageSpecification(userId), cancellationToken);
        if (image?.StorageKey is null)
        {
            return Result<StoredFileResponse>.Fail(LawyerErrors.ProfileImageNotFound);
        }

        var stored = await storedFileReader.OpenReadAsync(
            image.StorageKey,
            image.FileName,
            image.ContentType,
            cancellationToken);
        return stored is null
            ? Result<StoredFileResponse>.Fail(LawyerErrors.ProfileImageNotFound)
            : Result<StoredFileResponse>.Ok(new StoredFileResponse(stored.Content, stored.ContentType, stored.FileName, stored.Length));
    }
}

internal sealed class OwnProfileImageSpecification : Specification<LawyerProfile, ProfileImageStorageSnapshot>
{
    public OwnProfileImageSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId);
        UseNoTracking();
        Select(profile => new ProfileImageStorageSnapshot(
            profile.ProfileImageStorageKey,
            $"lawyer-{profile.Id}-profile-image",
            "application/octet-stream"));
    }
}

internal sealed record ProfileImageStorageSnapshot(string? StorageKey, string FileName, string ContentType);
