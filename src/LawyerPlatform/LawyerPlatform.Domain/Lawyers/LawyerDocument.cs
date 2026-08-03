using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;

namespace LawyerPlatform.Domain.Lawyers;

public sealed class LawyerDocument : Entity<Guid>, IAuditableEntity, ISoftDeleteEntity
{
    private LawyerDocument()
    {
    }

    internal LawyerDocument(
        Guid lawyerProfileId,
        string documentType,
        string storageKey,
        string originalFileName,
        string contentType,
        long fileSize,
        DateTime uploadedOnUtc)
        : base(Guid.NewGuid())
    {
        LawyerProfileId = lawyerProfileId;
        DocumentType = documentType.Trim();
        StorageKey = storageKey;
        OriginalFileName = originalFileName.Trim();
        ContentType = contentType.Trim();
        FileSize = fileSize;
        UploadedOnUtc = uploadedOnUtc;
    }

    public Guid LawyerProfileId { get; private set; }
    public LawyerProfile LawyerProfile { get; private set; } = null!;
    public string DocumentType { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public DateTime UploadedOnUtc { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public DateTime? RestoredOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    internal void SoftDelete(DateTime nowUtc)
    {
        IsDeleted = true;
        DeletedOnUtc = nowUtc;
        RestoredOnUtc = null;
        ModifiedOnUtc = nowUtc;
    }
}
