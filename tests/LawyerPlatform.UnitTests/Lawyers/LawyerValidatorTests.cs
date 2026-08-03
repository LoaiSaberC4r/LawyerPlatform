using LawyerPlatform.Application.Features.AdminLawyers.GetLawyers;
using LawyerPlatform.Application.Features.AdminLawyers.RejectLawyer;
using LawyerPlatform.Application.Features.Lawyers.ReplaceSpecializations;
using LawyerPlatform.Application.Features.Lawyers.UpdateOwnProfile;
using LawyerPlatform.Application.Features.PublicLawyers.SearchLawyers;

namespace LawyerPlatform.UnitTests.Lawyers;

public sealed class LawyerValidatorTests
{
    [Fact]
    public void UpdateProfile_RejectsInvalidFieldsAndRowVersion()
    {
        var result = new UpdateOwnProfileCommandValidator().Validate(new UpdateOwnProfileCommand(
            string.Empty,
            new string('x', 201),
            new string('x', 4001),
            -1,
            new string('x', 101),
            "not-base64"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "FullName");
        Assert.Contains(result.Errors, error => error.PropertyName == "YearsOfExperience");
        Assert.Contains(result.Errors, error => error.ErrorCode == "Lawyer.InvalidRowVersion");
    }

    [Fact]
    public void ReplaceSpecializations_RejectsDuplicates()
    {
        var result = new ReplaceSpecializationsCommandValidator().Validate(
            new ReplaceSpecializationsCommand([1, 1], Convert.ToBase64String(new byte[8])));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorCode == "Lawyer.DuplicateSpecialization");
    }

    [Fact]
    public void AdminDecision_RequiresReasonAndValidRowVersion()
    {
        var result = new RejectLawyerCommandValidator().Validate(
            new RejectLawyerCommand(Guid.NewGuid(), string.Empty, "bad"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorCode == "Lawyer.ApprovalReasonRequired");
        Assert.Contains(result.Errors, error => error.ErrorCode == "Lawyer.InvalidRowVersion");
    }

    [Fact]
    public void Pagination_IsBoundedForAdminAndPublicQueries()
    {
        var admin = new GetLawyersQueryValidator().Validate(new GetLawyersQuery(null, null, null, null, 0, 101));
        var search = new SearchLawyersQueryValidator().Validate(new SearchLawyersQuery(null, null, null, null, null, 0, 101));

        Assert.False(admin.IsValid);
        Assert.False(search.IsValid);
        Assert.Contains(admin.Errors, error => error.PropertyName == "PageSize");
        Assert.Contains(search.Errors, error => error.PropertyName == "PageNumber");
    }
}
