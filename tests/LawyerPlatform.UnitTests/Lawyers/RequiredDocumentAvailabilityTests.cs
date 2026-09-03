using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;

namespace LawyerPlatform.UnitTests.Lawyers;

public sealed class RequiredDocumentAvailabilityTests
{
    [Fact]
    public async Task GetAvailableTypesAsync_MissingPhysicalFile_DoesNotSatisfyRequiredDocument()
    {
        var policy = new RequiredDocumentPolicy();
        var availability = new TestStoredFileAvailability(
            ["lawyers/lawyer/documents/identity.pdf"]);
        StoredDocumentReference[] documents =
        [
            new("IdentityVerification", "lawyers/lawyer/documents/identity.pdf"),
            new("ProfessionalMembership", "lawyers/lawyer/documents/membership.pdf")
        ];

        var result = await RequiredDocumentAvailability.GetAvailableTypesAsync(
            documents,
            policy,
            availability,
            TestContext.Current.CancellationToken);

        Assert.Equal(["IdentityVerification"], result);
    }

    private sealed class RequiredDocumentPolicy : ILawyerDocumentPolicy
    {
        public IReadOnlyList<string> RequiredDocumentTypes =>
            ["IdentityVerification", "ProfessionalMembership"];

        public Task<BuildingBlock.Domain.Results.Result> ValidateAsync(
            string documentType,
            BuildingBlock.Application.Abstraction.Media.MediaUpload file,
            CancellationToken cancellationToken = default)
            => Task.FromResult(BuildingBlock.Domain.Results.Result.Ok());
    }

    private sealed class TestStoredFileAvailability(IReadOnlyCollection<string> availableKeys)
        : IStoredFileAvailability
    {
        public Task<bool> ExistsAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(availableKeys.Contains(storageKey, StringComparer.Ordinal));
        }
    }
}
