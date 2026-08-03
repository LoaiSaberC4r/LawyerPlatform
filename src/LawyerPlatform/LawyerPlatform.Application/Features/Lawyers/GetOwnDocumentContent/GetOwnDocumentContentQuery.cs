using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.GetOwnDocumentContent;

public sealed record GetOwnDocumentContentQuery(Guid DocumentId) : IQuery<StoredFileResponse>;

internal sealed class GetOwnDocumentContentQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    IStoredFileReader storedFileReader)
    : IQueryHandler<GetOwnDocumentContentQuery, StoredFileResponse>
{
    public async Task<Result<StoredFileResponse>> Handle(GetOwnDocumentContentQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<StoredFileResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var document = await repository.FirstOrDefaultAsync(
            new OwnDocumentStorageSpecification(userId, request.DocumentId),
            cancellationToken);
        if (document is null)
        {
            return Result<StoredFileResponse>.Fail(LawyerErrors.DocumentNotFound);
        }

        var stored = await storedFileReader.OpenReadAsync(
            document.StorageKey,
            document.OriginalFileName,
            document.ContentType,
            cancellationToken);
        return stored is null
            ? Result<StoredFileResponse>.Fail(LawyerErrors.DocumentNotFound)
            : Result<StoredFileResponse>.Ok(new StoredFileResponse(stored.Content, stored.ContentType, stored.FileName, stored.Length));
    }
}

internal sealed class OwnDocumentStorageSpecification : Specification<LawyerProfile, DocumentStorageSnapshot>
{
    public OwnDocumentStorageSpecification(Guid userAccountId, Guid documentId)
    {
        AddCriteria(profile =>
            profile.UserAccountId == userAccountId &&
            profile.Documents.Any(document => document.Id == documentId && !document.IsDeleted));
        UseNoTracking();
        Select(profile => profile.Documents
            .Where(document => document.Id == documentId && !document.IsDeleted)
            .Select(document => new DocumentStorageSnapshot(
                document.Id,
                document.StorageKey,
                document.OriginalFileName,
                document.ContentType,
                document.FileSize))
            .Single());
    }
}

internal sealed record DocumentStorageSnapshot(
    Guid Id,
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    long FileSize);
