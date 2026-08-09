using LawyerPlatform.Domain.Consultations;
using LawyerPlatform.Infrastructure.Consultations;

namespace LawyerPlatform.UnitTests.Consultations;

public sealed class ConsultationRequestTests
{
    private static readonly Guid ClientId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LawyerId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly DateTime NowUtc = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GuestAndClientFactoriesEnforceExactlyOneSourceAndStartNew()
    {
        var guest = CreateGuest();
        var client = CreateClient();
        var mixed = ConsultationRequest.Create(
            "CR-MIXED",
            ClientId,
            "Guest Name",
            "01000000000",
            null,
            LawyerId,
            null,
            "Description",
            null,
            NowUtc);
        var empty = ConsultationRequest.Create(
            "CR-EMPTY",
            null,
            null,
            null,
            null,
            LawyerId,
            null,
            "Description",
            null,
            NowUtc);

        Assert.Equal(ConsultationRequestStatus.New, guest.Status);
        Assert.Null(guest.ClientProfileId);
        Assert.Equal("Guest Name", guest.GuestFullName);
        Assert.Equal(ClientId, client.ClientProfileId);
        Assert.Null(client.GuestFullName);
        Assert.Contains(mixed.Errors, error => error.Code == "ConsultationRequest.InvalidSource");
        Assert.Contains(empty.Errors, error => error.Code == "ConsultationRequest.InvalidSource");
    }

    [Theory]
    [InlineData(ConsultationRequestStatus.New, ConsultationRequestStatus.UnderReview)]
    [InlineData(ConsultationRequestStatus.New, ConsultationRequestStatus.Approved)]
    [InlineData(ConsultationRequestStatus.New, ConsultationRequestStatus.Rejected)]
    [InlineData(ConsultationRequestStatus.UnderReview, ConsultationRequestStatus.Approved)]
    [InlineData(ConsultationRequestStatus.UnderReview, ConsultationRequestStatus.Rejected)]
    [InlineData(ConsultationRequestStatus.Approved, ConsultationRequestStatus.Completed)]
    public void EveryAllowedTransitionSucceedsAndCreatesHistory(
        ConsultationRequestStatus oldStatus,
        ConsultationRequestStatus newStatus)
    {
        var request = CreateInStatus(oldStatus);

        var result = request.ChangeStatus(
            newStatus,
            ActorId,
            newStatus == ConsultationRequestStatus.Rejected ? "Valid reason" : null,
            NowUtc.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(newStatus, request.Status);
        var history = request.StatusHistory.Last();
        Assert.Equal(oldStatus, history.OldStatus);
        Assert.Equal(newStatus, history.NewStatus);
        Assert.Equal(ActorId, history.ChangedByUserId);
    }

    [Theory]
    [InlineData(ConsultationRequestStatus.New, ConsultationRequestStatus.Completed)]
    [InlineData(ConsultationRequestStatus.UnderReview, ConsultationRequestStatus.Completed)]
    [InlineData(ConsultationRequestStatus.Approved, ConsultationRequestStatus.Rejected)]
    [InlineData(ConsultationRequestStatus.Rejected, ConsultationRequestStatus.Approved)]
    [InlineData(ConsultationRequestStatus.Completed, ConsultationRequestStatus.UnderReview)]
    public void UnsupportedAndTerminalTransitionsAreRejected(
        ConsultationRequestStatus oldStatus,
        ConsultationRequestStatus newStatus)
    {
        var request = CreateInStatus(oldStatus);

        var result = request.ChangeStatus(newStatus, ActorId, "reason", NowUtc.AddHours(2));

        Assert.Contains(result.Errors, error => error.Code == "ConsultationRequest.InvalidStatusTransition");
    }

    [Fact]
    public void RejectionRequiresTrimmedReasonAndCompletionSetsUtcTimestamp()
    {
        var rejected = CreateGuest();
        var missingReason = rejected.ChangeStatus(
            ConsultationRequestStatus.Rejected,
            ActorId,
            "   ",
            NowUtc);
        Assert.Contains(missingReason.Errors, error => error.Code == "ConsultationRequest.RejectionReasonRequired");

        Assert.True(rejected.ChangeStatus(
            ConsultationRequestStatus.Rejected,
            ActorId,
            "  Outside scope  ",
            NowUtc).IsSuccess);
        Assert.Equal("Outside scope", rejected.StatusHistory.Single().Reason);
        Assert.Null(rejected.CompletedOnUtc);

        var completed = CreateInStatus(ConsultationRequestStatus.Approved);
        Assert.True(completed.ChangeStatus(
            ConsultationRequestStatus.Completed,
            ActorId,
            null,
            NowUtc).IsSuccess);
        Assert.Equal(NowUtc, completed.CompletedOnUtc);
        Assert.Equal(DateTimeKind.Utc, completed.CompletedOnUtc!.Value.Kind);
    }

    [Fact]
    public void ReferenceGeneratorProducesNonSequentialUniquePublicValues()
    {
        var generator = new ConsultationReferenceNumberGenerator();
        var values = Enumerable.Range(0, 1000).Select(_ => generator.Generate()).ToArray();

        Assert.Equal(values.Length, values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(values, value =>
        {
            Assert.StartsWith("CR-", value, StringComparison.Ordinal);
            Assert.InRange(value.Length, 4, ConsultationRequest.MaximumReferenceNumberLength);
        });
    }

    private static ConsultationRequest CreateGuest()
        => ConsultationRequest.CreateForGuest(
            $"CR-{Guid.NewGuid():N}"[..23],
            "Guest Name",
            "01000000000",
            "guest@example.test",
            LawyerId,
            1,
            "Description",
            null,
            NowUtc).Value;

    private static ConsultationRequest CreateClient()
        => ConsultationRequest.CreateForClient(
            $"CR-{Guid.NewGuid():N}"[..23],
            ClientId,
            LawyerId,
            null,
            "Description",
            null,
            NowUtc).Value;

    private static ConsultationRequest CreateInStatus(ConsultationRequestStatus status)
    {
        var request = CreateGuest();
        switch (status)
        {
            case ConsultationRequestStatus.New:
                break;
            case ConsultationRequestStatus.UnderReview:
                request.ChangeStatus(status, ActorId, null, NowUtc.AddMinutes(1));
                break;
            case ConsultationRequestStatus.Approved:
                request.ChangeStatus(status, ActorId, null, NowUtc.AddMinutes(1));
                break;
            case ConsultationRequestStatus.Rejected:
                request.ChangeStatus(status, ActorId, "reason", NowUtc.AddMinutes(1));
                break;
            case ConsultationRequestStatus.Completed:
                request.ChangeStatus(ConsultationRequestStatus.Approved, ActorId, null, NowUtc.AddMinutes(1));
                request.ChangeStatus(status, ActorId, null, NowUtc.AddMinutes(2));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        return request;
    }
}
