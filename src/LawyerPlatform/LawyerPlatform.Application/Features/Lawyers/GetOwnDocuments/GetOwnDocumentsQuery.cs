using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.GetOwnDocuments;

public sealed record GetOwnDocumentsQuery : IQuery<IReadOnlyList<LawyerDocumentResponse>>;

internal sealed class GetOwnDocumentsQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository)
    : IQueryHandler<GetOwnDocumentsQuery, IReadOnlyList<LawyerDocumentResponse>>
{
    public async Task<Result<IReadOnlyList<LawyerDocumentResponse>>> Handle(GetOwnDocumentsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<IReadOnlyList<LawyerDocumentResponse>>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var documents = await repository.FirstOrDefaultAsync(new OwnDocumentsSpecification(userId), cancellationToken);
        return documents is null
            ? Result<IReadOnlyList<LawyerDocumentResponse>>.Fail(LawyerErrors.NotFound)
            : Result<IReadOnlyList<LawyerDocumentResponse>>.Ok(documents.Select(DocumentMapper.Map).ToArray());
    }
}

internal static class DocumentMapper
{
    public static LawyerDocumentResponse Map(DocumentMetadataSnapshot document) => new(
        document.Id,
        document.DocumentType,
        document.OriginalFileName,
        document.ContentType,
        document.FileSize,
        document.UploadedOnUtc,
        $"/api/v1/lawyer/documents/{document.Id}/content",
        RowVersionCodec.Encode(document.RowVersion));
}

internal sealed class OwnDocumentsSpecification : Specification<LawyerProfile, IReadOnlyList<DocumentMetadataSnapshot>>
{
    public OwnDocumentsSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId);
        UseNoTracking();
        Select(profile => profile.Documents
            .Where(document => !document.IsDeleted)
            .OrderByDescending(document => document.UploadedOnUtc)
            .ThenBy(document => document.Id)
            .Select(document => new DocumentMetadataSnapshot(
                document.Id,
                document.DocumentType,
                document.OriginalFileName,
                document.ContentType,
                document.FileSize,
                document.UploadedOnUtc,
                document.RowVersion))
            .ToArray());
    }
}

internal sealed record DocumentMetadataSnapshot(
    Guid Id,
    string DocumentType,
    string OriginalFileName,
    string ContentType,
    long FileSize,
    DateTime UploadedOnUtc,
    byte[] RowVersion);
