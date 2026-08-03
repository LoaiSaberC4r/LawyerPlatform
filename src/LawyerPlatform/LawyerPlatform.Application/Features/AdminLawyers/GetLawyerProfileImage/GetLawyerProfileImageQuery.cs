using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.AdminLawyers.GetLawyerProfileImage;

public sealed record GetLawyerProfileImageQuery(Guid LawyerId) : IQuery<StoredFileResponse>;

internal sealed class GetLawyerProfileImageQueryHandler(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    IStoredFileReader storedFileReader)
    : IQueryHandler<GetLawyerProfileImageQuery, StoredFileResponse>
{
    public async Task<Result<StoredFileResponse>> Handle(GetLawyerProfileImageQuery query, CancellationToken cancellationToken)
    {
        var image = await repository.FirstOrDefaultAsync(new AdminProfileImageSpecification(query.LawyerId), cancellationToken);
        if (image?.StorageKey is null)
        {
            return Result<StoredFileResponse>.Fail(LawyerErrors.ProfileImageNotFound);
        }

        var stored = await storedFileReader.OpenReadAsync(
            image.StorageKey,
            image.FileName,
            "application/octet-stream",
            cancellationToken);
        return stored is null
            ? Result<StoredFileResponse>.Fail(LawyerErrors.ProfileImageNotFound)
            : Result<StoredFileResponse>.Ok(new StoredFileResponse(stored.Content, stored.ContentType, stored.FileName, stored.Length));
    }
}

internal sealed class AdminProfileImageSpecification : Specification<LawyerProfile, AdminProfileImageSnapshot>
{
    public AdminProfileImageSpecification(Guid lawyerId)
    {
        AddCriteria(profile => profile.Id == lawyerId);
        UseNoTracking();
        Select(profile => new AdminProfileImageSnapshot(
            profile.ProfileImageStorageKey,
            $"lawyer-{profile.Id}-profile-image"));
    }
}

internal sealed record AdminProfileImageSnapshot(string? StorageKey, string FileName);
