using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.AdminLawyers.GetLawyerDocumentContent;

public sealed record GetLawyerDocumentContentQuery(Guid LawyerId, Guid DocumentId) : IQuery<StoredFileResponse>;

internal sealed class GetLawyerDocumentContentQueryHandler(
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    IStoredFileReader storedFileReader)
    : IQueryHandler<GetLawyerDocumentContentQuery, StoredFileResponse>
{
    public async Task<Result<StoredFileResponse>> Handle(GetLawyerDocumentContentQuery query, CancellationToken cancellationToken)
    {
        var document = await repository.FirstOrDefaultAsync(
            new AdminDocumentStorageSpecification(query.LawyerId, query.DocumentId),
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

internal sealed class AdminDocumentStorageSpecification : Specification<LawyerProfile, AdminDocumentStorageSnapshot>
{
    public AdminDocumentStorageSpecification(Guid lawyerId, Guid documentId)
    {
        AddCriteria(profile =>
            profile.Id == lawyerId &&
            profile.Documents.Any(document => document.Id == documentId && !document.IsDeleted));
        UseNoTracking();
        Select(profile => profile.Documents
            .Where(document => document.Id == documentId && !document.IsDeleted)
            .Select(document => new AdminDocumentStorageSnapshot(
                document.StorageKey,
                document.OriginalFileName,
                document.ContentType))
            .Single());
    }
}

internal sealed record AdminDocumentStorageSnapshot(string StorageKey, string OriginalFileName, string ContentType);
