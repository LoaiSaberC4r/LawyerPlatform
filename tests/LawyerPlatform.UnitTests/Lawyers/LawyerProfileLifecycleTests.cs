using LawyerPlatform.Domain.Accounts;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.UnitTests.Lawyers;

public sealed class LawyerProfileLifecycleTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LawyerUserId = Guid.Parse("11111111-1111-1111-1111-111111111112");
    private static readonly Guid AdminUserId = Guid.Parse("11111111-1111-1111-1111-111111111113");

    public static TheoryData<decimal, decimal, string> InvalidCoordinateRanges => new()
    {
        { -90.000001m, 31.235712m, "Lawyer.InvalidLatitude" },
        { 90.000001m, 31.235712m, "Lawyer.InvalidLatitude" },
        { 30.044420m, -180.000001m, "Lawyer.InvalidLongitude" },
        { 30.044420m, 180.000001m, "Lawyer.InvalidLongitude" }
    };

    public static TheoryData<decimal?, decimal?> PartialCoordinatePairs => new()
    {
        { 30.044420m, null },
        { null, 31.235712m }
    };

    [Fact]
    public void NewLawyerProfile_StartsDraftAndEmpty()
    {
        var profile = CreateProfile();

        Assert.Equal(LawyerApprovalStatus.Draft, profile.ApprovalStatus);
        Assert.Empty(profile.Offices);
        Assert.Empty(profile.Specializations);
        Assert.Empty(profile.Documents);
        Assert.Empty(profile.StatusHistory);
    }

    [Fact]
    public void IncompleteDraft_CannotSubmit()
    {
        var profile = CreateProfile();

        var result = profile.SubmitForApproval(LawyerUserId, true, false, NowUtc);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "Lawyer.ProfileIncomplete");
        Assert.Equal(LawyerApprovalStatus.Draft, profile.ApprovalStatus);
        Assert.Empty(profile.StatusHistory);
    }

    [Fact]
    public void CompleteLifecycle_CreatesAppendOnlyUtcHistory()
    {
        var profile = CreateCompleteProfile();

        Assert.True(profile.SubmitForApproval(LawyerUserId, true, true, NowUtc).IsSuccess);
        Assert.True(profile.RequestChanges(AdminUserId, "Add clarification", NowUtc.AddMinutes(1)).IsSuccess);
        Assert.True(profile.UpdateProfessionalProfile(
            profile.FullName,
            profile.ProfessionalTitle,
            "Clarified biography",
            profile.YearsOfExperience,
            profile.ProfessionalRegistrationNumber).IsSuccess);
        Assert.True(profile.SubmitForApproval(LawyerUserId, true, true, NowUtc.AddMinutes(2)).IsSuccess);
        Assert.True(profile.Approve(AdminUserId, true, NowUtc.AddMinutes(3)).IsSuccess);
        Assert.True(profile.Suspend(AdminUserId, "Compliance review", NowUtc.AddMinutes(4)).IsSuccess);
        Assert.True(profile.Reactivate(AdminUserId, true, NowUtc.AddMinutes(5)).IsSuccess);

        Assert.Equal(LawyerApprovalStatus.Approved, profile.ApprovalStatus);
        Assert.Equal(6, profile.StatusHistory.Count);
        Assert.All(profile.StatusHistory, history => Assert.Equal(DateTimeKind.Utc, history.ChangedOnUtc.Kind));
        Assert.Contains(profile.StatusHistory, history =>
            history.NewStatus == LawyerApprovalStatus.ChangesRequested &&
            history.ChangedByUserId == AdminUserId &&
            history.Reason == "Add clarification");
    }

    [Fact]
    public void PendingProfile_IsReadOnlyAndUnsupportedTransitionsFail()
    {
        var profile = CreateCompleteProfile();
        profile.SubmitForApproval(LawyerUserId, true, true, NowUtc);

        var edit = profile.UpdateProfessionalProfile("Changed", "Title", null, 2, "REG-2");
        var suspend = profile.Suspend(AdminUserId, "reason", NowUtc);

        Assert.True(edit.IsFailure);
        Assert.Contains(edit.Errors, error => error.Code == "Lawyer.ProfileEditNotAllowed");
        Assert.True(suspend.IsFailure);
        Assert.Contains(suspend.Errors, error => error.Code == "Lawyer.InvalidApprovalStatus");
    }

    [Fact]
    public void ApprovedProfile_AllowsPublicEditsButProtectsIdentityFields()
    {
        var profile = CreateApprovedProfile();

        var allowed = profile.UpdateProfessionalProfile(
            profile.FullName,
            profile.ProfessionalTitle,
            "Updated biography",
            15,
            profile.ProfessionalRegistrationNumber);
        var forbidden = profile.UpdateProfessionalProfile(
            "Different Name",
            profile.ProfessionalTitle,
            profile.Biography,
            profile.YearsOfExperience,
            profile.ProfessionalRegistrationNumber);

        Assert.True(allowed.IsSuccess);
        Assert.Equal(LawyerApprovalStatus.Approved, profile.ApprovalStatus);
        Assert.True(forbidden.IsFailure);
        Assert.Contains(forbidden.Errors, error => error.Code == "Lawyer.SensitiveProfileEditNotAllowed");
    }

    [Fact]
    public void ApprovedProfile_CannotRemoveLastRequiredDocument()
    {
        var profile = CreateApprovedProfile();
        var document = Assert.Single(profile.Documents);

        var result = profile.RemoveDocument(document.Id, ["IdentityVerification"], NowUtc.AddMinutes(4));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "Lawyer.RequiredDocumentCannotBeRemoved");
        Assert.False(document.IsDeleted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RequiredReasons_AreEnforced(string reason)
    {
        var pending = CreateCompleteProfile();
        pending.SubmitForApproval(LawyerUserId, true, true, NowUtc);
        var approved = CreateApprovedProfile();

        Assert.Contains(pending.Reject(AdminUserId, reason, NowUtc).Errors, error => error.Code == "Lawyer.ApprovalReasonRequired");
        Assert.Contains(pending.RequestChanges(AdminUserId, reason, NowUtc).Errors, error => error.Code == "Lawyer.ApprovalReasonRequired");
        Assert.Contains(approved.Suspend(AdminUserId, reason, NowUtc).Errors, error => error.Code == "Lawyer.SuspensionReasonRequired");
    }

    [Fact]
    public void PublicEligibility_RequiresApprovedActiveCompleteAndNotDeleted()
    {
        var profile = CreateApprovedProfile();

        Assert.True(profile.CanAppearPublicly(true, true));
        Assert.False(profile.CanAppearPublicly(false, true));
        Assert.False(profile.CanAppearPublicly(true, false));
        profile.IsDeleted = true;
        Assert.False(profile.CanAppearPublicly(true, true));
    }

    [Fact]
    public void PrimaryOffice_AcceptsValidAndNullCoordinatePairs()
    {
        var withCoordinates = CreateProfile();
        var coordinatesResult = withCoordinates.UpsertPrimaryOffice(
            1, 10, 100, "Detailed address", null, 30.044420m, 31.235712m);
        var withoutCoordinates = CreateProfile();
        var nullResult = withoutCoordinates.UpsertPrimaryOffice(
            1, 10, 100, "Detailed address", null, null, null);

        Assert.True(coordinatesResult.IsSuccess);
        Assert.Equal(30.044420m, coordinatesResult.Value.Latitude);
        Assert.Equal(31.235712m, coordinatesResult.Value.Longitude);
        Assert.True(nullResult.IsSuccess);
        Assert.Null(nullResult.Value.Latitude);
        Assert.Null(nullResult.Value.Longitude);
    }

    [Theory]
    [MemberData(nameof(InvalidCoordinateRanges))]
    public void PrimaryOffice_RejectsCoordinatesOutsideValidRanges(
        decimal latitude,
        decimal longitude,
        string expectedErrorCode)
    {
        var result = CreateProfile().UpsertPrimaryOffice(
            1, 10, 100, "Detailed address", null, latitude, longitude);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == expectedErrorCode);
    }

    [Theory]
    [MemberData(nameof(PartialCoordinatePairs))]
    public void PrimaryOffice_RejectsPartialCoordinatePairs(decimal? latitude, decimal? longitude)
    {
        var result = CreateProfile().UpsertPrimaryOffice(
            1, 10, 100, "Detailed address", null, latitude, longitude);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "Lawyer.InvalidOfficeCoordinates");
    }

    [Fact]
    public void PrimaryOffice_UpdateChangesCoordinatesAndPreservesIdentity()
    {
        var profile = CreateProfile();
        var created = profile.UpsertPrimaryOffice(
            1, 10, 100, "Detailed address", null, 30.044420m, 31.235712m).Value;

        var updated = profile.UpsertPrimaryOffice(
            1, 10, 100, "Updated address", null, 29.975300m, 31.137600m);

        Assert.True(updated.IsSuccess);
        Assert.Equal(created.Id, updated.Value.Id);
        Assert.Equal(29.975300m, updated.Value.Latitude);
        Assert.Equal(31.137600m, updated.Value.Longitude);
        Assert.Single(profile.Offices);
    }

    private static LawyerProfile CreateApprovedProfile()
    {
        var profile = CreateCompleteProfile();
        profile.SubmitForApproval(LawyerUserId, true, true, NowUtc);
        profile.Approve(AdminUserId, true, NowUtc.AddMinutes(1));
        return profile;
    }

    private static LawyerProfile CreateCompleteProfile()
    {
        var profile = CreateProfile();
        Assert.True(profile.UpdateProfessionalProfile("Lawyer One", "Attorney", "Biography", 12, "REG-1").IsSuccess);
        Assert.True(profile.UpsertPrimaryOffice(1, 10, 100, "Detailed address", "01000000000").IsSuccess);
        Assert.True(profile.ReplaceSpecializations([1]).IsSuccess);
        Assert.True(profile.AddDocument(
            "IdentityVerification",
            "lawyers/test/document.pdf",
            "document.pdf",
            "application/pdf",
            100,
            NowUtc).IsSuccess);
        return profile;
    }

    private static LawyerProfile CreateProfile()
    {
        var account = UserAccount.CreateLawyer(
            "lawyer.one",
            "LAWYER.ONE",
            "lawyer@example.test",
            "LAWYER@EXAMPLE.TEST",
            "01000000000",
            "hash",
            NowUtc).Value;
        return LawyerProfile.Create(account, "Lawyer One").Value;
    }
}
