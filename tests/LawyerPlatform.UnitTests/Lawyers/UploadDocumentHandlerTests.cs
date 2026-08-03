using System.Linq.Expressions;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.UploadDocument;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LawyerPlatform.UnitTests.Lawyers;

public sealed class UploadDocumentHandlerTests
{
    [Fact]
    public async Task PersistenceFailure_RemovesNewlyStoredFile()
    {
        var nowUtc = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);
        var account = UserAccount.CreateLawyer(
            "upload.lawyer",
            "UPLOAD.LAWYER",
            "upload@example.test",
            "UPLOAD@EXAMPLE.TEST",
            "01000000001",
            "hash",
            nowUtc).Value;
        var profile = LawyerProfile.Create(account, "Upload Lawyer").Value;
        var repository = new FakeWriteRepository(profile);
        var unitOfWork = new FailingUnitOfWork(repository);
        var mediaService = new RecordingMediaService();
        var handler = new UploadDocumentCommandHandler(
            new FakeCurrentUser(account.Id),
            unitOfWork,
            new AllowDocumentPolicy(),
            mediaService,
            new FixedClock(nowUtc),
            new RecordingAggregatePersistence(),
            NullLogger<UploadDocumentCommandHandler>.Instance);
        await using var stream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UploadDocumentCommand(
                "IdentityVerification",
                new MediaUpload(stream, "identity.pdf", "application/pdf", stream.Length)),
            TestContext.Current.CancellationToken));

        Assert.Equal("lawyers/generated/document.pdf", mediaService.DeletedKey);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => userId;
        public string? UserName => "upload.lawyer";
        public string? Email => "upload@example.test";
        public IReadOnlyCollection<string> Roles => ["Lawyer"];
        public IReadOnlyCollection<string> Permissions => [];
        public string? GetClaimValue(string claimType) => null;
        public IReadOnlyCollection<string> GetClaimValues(string claimType) => [];
    }

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class AllowDocumentPolicy : ILawyerDocumentPolicy
    {
        public IReadOnlyList<string> RequiredDocumentTypes => ["IdentityVerification"];

        public Task<Result> ValidateAsync(string documentType, MediaUpload upload, CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());
    }

    private sealed class RecordingMediaService : IMediaService
    {
        public string? DeletedKey { get; private set; }

        public Task<StoredMedia> SaveAsync(MediaUpload upload, MediaStorageRequest request, CancellationToken ct = default)
            => Task.FromResult(new StoredMedia(
                "lawyers/generated/document.pdf",
                "document.pdf",
                "application/pdf",
                ".pdf",
                upload.Length!.Value));

        public Task<IReadOnlyList<StoredMedia>> SaveAsync(
            IEnumerable<MediaUpload> uploads,
            MediaStorageRequest request,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            DeletedKey = key;
            return Task.CompletedTask;
        }

        public Task DeleteRangeAsync(IEnumerable<string> keys, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingAggregatePersistence : ILawyerAggregatePersistence
    {
        public void Add<TEntity>(TEntity entity) where TEntity : class
        {
        }

        public void AddRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
        }

        public void RemoveRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
        }
    }

    private sealed class FailingUnitOfWork(FakeWriteRepository repository) : IUnitOfWork<LawyerPlatformWritePersistence>
    {
        public int SaveCalls { get; private set; }

        public IWriteRepository<TEntity, LawyerPlatformWritePersistence> WriteRepository<TEntity>() where TEntity : class, BuildingBlock.Domain.Primitive.IAggregateRoot
            => (IWriteRepository<TEntity, LawyerPlatformWritePersistence>)(object)repository;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            throw new InvalidOperationException("Simulated persistence failure.");
        }

        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeWriteRepository(LawyerProfile profile)
        : IWriteRepository<LawyerProfile, LawyerPlatformWritePersistence>
    {
        public Task<LawyerProfile?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
            where TKey : notnull
            => Task.FromResult<LawyerProfile?>(profile);

        public Task<LawyerProfile?> GetByPropertyAsync(
            Expression<Func<LawyerProfile, bool>> predicate,
            CancellationToken cancellationToken = default)
            => Task.FromResult<LawyerProfile?>(profile);

        public Task<LawyerProfile?> FirstOrDefaultAsync(
            Specification<LawyerProfile> specification,
            CancellationToken cancellationToken = default)
            => Task.FromResult<LawyerProfile?>(profile);

        public Task AddAsync(LawyerProfile entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<LawyerProfile> entities, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Update(LawyerProfile entity) { }
        public void UpdateRange(IEnumerable<LawyerProfile> entities) { }
        public void Delete(LawyerProfile entity) { }
        public void DeleteRange(IEnumerable<LawyerProfile> entities) { }
    }
}
