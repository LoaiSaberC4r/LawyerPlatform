using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Application.Features.Lawyers.Common;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Application.Features.Lawyers.GetApprovalStatus;

public sealed record GetApprovalStatusQuery : IQuery<LawyerApprovalStatusResponse>;

public sealed record LawyerApprovalStatusResponse(
    string ApprovalStatus,
    string? ApprovalReason,
    DateTime? SubmittedOnUtc,
    DateTime? ApprovedOnUtc,
    DateTime? SuspendedOnUtc,
    bool CanEdit,
    bool CanSubmitForApproval,
    LawyerProfileCompletionResponse Completion,
    string RowVersion);

internal sealed class GetApprovalStatusQueryHandler(
    ICurrentUser currentUser,
    IReadRepository<LawyerProfile, LawyerPlatformReadPersistence> repository,
    ILawyerDocumentPolicy documentPolicy)
    : IQueryHandler<GetApprovalStatusQuery, LawyerApprovalStatusResponse>
{
    public async Task<Result<LawyerApprovalStatusResponse>> Handle(GetApprovalStatusQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<LawyerApprovalStatusResponse>.Fail(LawyerApplicationErrors.AccountNotFound);
        }

        var snapshot = await repository.FirstOrDefaultAsync(new ApprovalStatusSpecification(userId), cancellationToken);
        if (snapshot is null)
        {
            return Result<LawyerApprovalStatusResponse>.Fail(LawyerErrors.NotFound);
        }

        var completion = LawyerProfileCompletionCalculator.Calculate(
            snapshot.ApprovalStatus,
            snapshot.FullName,
            snapshot.ProfessionalTitle,
            snapshot.YearsOfExperience,
            snapshot.ProfessionalRegistrationNumber,
            snapshot.OfficeComplete,
            snapshot.SpecializationsComplete,
            snapshot.ActiveDocumentTypes,
            snapshot.AccountStatus == AccountStatus.Active,
            documentPolicy);

        return Result<LawyerApprovalStatusResponse>.Ok(new LawyerApprovalStatusResponse(
            snapshot.ApprovalStatus.ToString(),
            snapshot.ApprovalReason,
            snapshot.SubmittedOnUtc,
            snapshot.ApprovedOnUtc,
            snapshot.SuspendedOnUtc,
            snapshot.ApprovalStatus is LawyerApprovalStatus.Draft or LawyerApprovalStatus.ChangesRequested or LawyerApprovalStatus.Approved,
            completion.CanSubmitForApproval,
            completion,
            RowVersionCodec.Encode(snapshot.RowVersion)));
    }
}

internal sealed class ApprovalStatusSpecification : Specification<LawyerProfile, ApprovalStatusSnapshot>
{
    public ApprovalStatusSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId);
        UseNoTracking();
        Select(profile => new ApprovalStatusSnapshot(
            profile.ApprovalStatus,
            profile.ApprovalReason,
            profile.SubmittedOnUtc,
            profile.ApprovedOnUtc,
            profile.SuspendedOnUtc,
            profile.FullName,
            profile.ProfessionalTitle,
            profile.YearsOfExperience,
            profile.ProfessionalRegistrationNumber,
            profile.Offices.Any(office =>
                office.IsPrimary && office.IsActive &&
                office.Governorate.IsActive && office.City.IsActive && office.Area.IsActive &&
                office.City.GovernorateId == office.GovernorateId && office.Area.CityId == office.CityId &&
                office.DetailedAddress != ""),
            profile.Specializations.Any() && profile.Specializations.All(item => item.LegalSpecialization.IsActive),
            profile.Documents.Where(document => !document.IsDeleted).Select(document => document.DocumentType).ToArray(),
            profile.UserAccount.Status,
            profile.RowVersion));
    }
}

internal sealed record ApprovalStatusSnapshot(
    LawyerApprovalStatus ApprovalStatus,
    string? ApprovalReason,
    DateTime? SubmittedOnUtc,
    DateTime? ApprovedOnUtc,
    DateTime? SuspendedOnUtc,
    string FullName,
    string? ProfessionalTitle,
    int? YearsOfExperience,
    string? ProfessionalRegistrationNumber,
    bool OfficeComplete,
    bool SpecializationsComplete,
    IReadOnlyList<string> ActiveDocumentTypes,
    AccountStatus AccountStatus,
    byte[] RowVersion);
