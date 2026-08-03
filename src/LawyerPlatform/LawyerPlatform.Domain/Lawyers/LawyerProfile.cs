using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Accounts;

namespace LawyerPlatform.Domain.Lawyers;

public sealed class LawyerProfile : AggregateRoot<Guid>, IAuditableEntity, ISoftDeleteEntity
{
    private readonly List<LawyerOffice> _offices = [];
    private readonly List<LawyerDocument> _documents = [];
    private readonly List<LawyerSpecialization> _specializations = [];
    private readonly List<LawyerApprovalStatusHistory> _statusHistory = [];

    private LawyerProfile()
    {
    }

    private LawyerProfile(Guid id, UserAccount userAccount, string fullName)
        : base(id)
    {
        UserAccountId = userAccount.Id;
        UserAccount = userAccount;
        FullName = fullName.Trim();
        ApprovalStatus = LawyerApprovalStatus.Draft;
    }

    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public string FullName { get; private set; } = string.Empty;
    public string? ProfessionalTitle { get; private set; }
    public string? Biography { get; private set; }
    public int? YearsOfExperience { get; private set; }
    public string? ProfessionalRegistrationNumber { get; private set; }
    public string? ProfileImageStorageKey { get; private set; }
    public LawyerApprovalStatus ApprovalStatus { get; private set; }
    public string? ApprovalReason { get; private set; }
    public DateTime? SubmittedOnUtc { get; private set; }
    public DateTime? ApprovedOnUtc { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? SuspendedOnUtc { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
    public DateTime? RestoredOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<LawyerOffice> Offices => _offices.AsReadOnly();
    public IReadOnlyCollection<LawyerDocument> Documents => _documents.AsReadOnly();
    public IReadOnlyCollection<LawyerSpecialization> Specializations => _specializations.AsReadOnly();
    public IReadOnlyCollection<LawyerApprovalStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public static Result<LawyerProfile> Create(UserAccount userAccount, string fullName)
    {
        ArgumentNullException.ThrowIfNull(userAccount);

        if (userAccount.Role != AccountRole.Lawyer)
        {
            return Result<LawyerProfile>.Fail(Error.Domain("LawyerProfile.InvalidAccountRole", "A lawyer profile requires a lawyer account."));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return Result<LawyerProfile>.Fail(AccountErrors.FullNameRequired);
        }

        return Result<LawyerProfile>.Ok(new LawyerProfile(Guid.NewGuid(), userAccount, fullName));
    }

    public Result UpdateProfessionalProfile(
        string fullName,
        string? professionalTitle,
        string? biography,
        int? yearsOfExperience,
        string? professionalRegistrationNumber)
    {
        var editCheck = EnsureEditable();
        if (editCheck.IsFailure)
        {
            return editCheck;
        }

        var normalizedFullName = fullName.Trim();
        var normalizedTitle = Normalize(professionalTitle);
        var normalizedBiography = Normalize(biography);
        var normalizedRegistrationNumber = Normalize(professionalRegistrationNumber);

        if (ApprovalStatus == LawyerApprovalStatus.Approved &&
            (!string.Equals(FullName, normalizedFullName, StringComparison.Ordinal) ||
             !string.Equals(ProfessionalTitle, normalizedTitle, StringComparison.Ordinal) ||
             !string.Equals(ProfessionalRegistrationNumber, normalizedRegistrationNumber, StringComparison.Ordinal)))
        {
            return Result.Fail(LawyerErrors.SensitiveProfileEditNotAllowed);
        }

        if (string.IsNullOrWhiteSpace(normalizedFullName) || normalizedFullName.Length > 200 ||
            normalizedTitle?.Length > 200 || normalizedBiography?.Length > 4000 ||
            yearsOfExperience < 0 || normalizedRegistrationNumber?.Length > 100)
        {
            return Result.Fail(Error.Validation("Lawyer.InvalidProfessionalProfile", "The professional profile is invalid."));
        }

        FullName = normalizedFullName;
        ProfessionalTitle = normalizedTitle;
        Biography = normalizedBiography;
        YearsOfExperience = yearsOfExperience;
        ProfessionalRegistrationNumber = normalizedRegistrationNumber;
        return Result.Ok();
    }

    public Result ReplaceProfileImage(string storageKey)
    {
        var editCheck = EnsureEditable();
        if (editCheck.IsFailure)
        {
            return editCheck;
        }

        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > 500)
        {
            return Result.Fail(LawyerErrors.InvalidDocument);
        }

        ProfileImageStorageKey = storageKey;
        return Result.Ok();
    }

    public Result<LawyerOffice> UpsertPrimaryOffice(
        int governorateId,
        int cityId,
        int areaId,
        string detailedAddress,
        string? publicPhoneNumber)
    {
        var editCheck = EnsureEditable(LawyerErrors.OfficeEditNotAllowed);
        if (editCheck.IsFailure)
        {
            return Result<LawyerOffice>.Fail(editCheck.Errors);
        }

        if (governorateId <= 0 || cityId <= 0 || areaId <= 0 ||
            string.IsNullOrWhiteSpace(detailedAddress) || detailedAddress.Trim().Length > 500 ||
            Normalize(publicPhoneNumber)?.Length > 30)
        {
            return Result<LawyerOffice>.Fail(Error.Validation("Lawyer.InvalidOffice", "The primary office is invalid."));
        }

        var office = _offices.SingleOrDefault(item => item.IsPrimary && item.IsActive);
        if (office is null)
        {
            office = new LawyerOffice(Id, governorateId, cityId, areaId, detailedAddress, publicPhoneNumber);
            _offices.Add(office);
        }
        else
        {
            office.Update(governorateId, cityId, areaId, detailedAddress, publicPhoneNumber);
        }

        return Result<LawyerOffice>.Ok(office);
    }

    public Result ReplaceSpecializations(IReadOnlyCollection<int> specializationIds)
    {
        ArgumentNullException.ThrowIfNull(specializationIds);

        var editCheck = EnsureEditable();
        if (editCheck.IsFailure)
        {
            return editCheck;
        }

        if (specializationIds.Count != specializationIds.Distinct().Count())
        {
            return Result.Fail(LawyerErrors.DuplicateSpecialization);
        }

        if (ApprovalStatus == LawyerApprovalStatus.Approved && specializationIds.Count == 0)
        {
            return Result.Fail(LawyerErrors.ProfileIncomplete);
        }

        _specializations.Clear();
        _specializations.AddRange(specializationIds.Select(id => new LawyerSpecialization(Id, id)));
        return Result.Ok();
    }

    public Result<LawyerDocument> AddDocument(
        string documentType,
        string storageKey,
        string originalFileName,
        string contentType,
        long fileSize,
        DateTime uploadedOnUtc)
    {
        var editCheck = EnsureEditable();
        if (editCheck.IsFailure)
        {
            return Result<LawyerDocument>.Fail(editCheck.Errors);
        }

        if (string.IsNullOrWhiteSpace(documentType) || documentType.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(storageKey) || storageKey.Length > 500 ||
            string.IsNullOrWhiteSpace(originalFileName) || originalFileName.Trim().Length > 255 ||
            string.IsNullOrWhiteSpace(contentType) || contentType.Trim().Length > 150 || fileSize <= 0)
        {
            return Result<LawyerDocument>.Fail(LawyerErrors.InvalidDocument);
        }

        var document = new LawyerDocument(
            Id,
            documentType,
            storageKey,
            originalFileName,
            contentType,
            fileSize,
            RequireUtc(uploadedOnUtc));
        _documents.Add(document);
        return Result<LawyerDocument>.Ok(document);
    }

    public Result RemoveDocument(Guid documentId, IReadOnlyCollection<string> requiredDocumentTypes, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(requiredDocumentTypes);

        var editCheck = EnsureEditable();
        if (editCheck.IsFailure)
        {
            return editCheck;
        }

        var document = _documents.SingleOrDefault(item => item.Id == documentId && !item.IsDeleted);
        if (document is null)
        {
            return Result.Fail(LawyerErrors.DocumentNotFound);
        }

        if (ApprovalStatus == LawyerApprovalStatus.Approved &&
            requiredDocumentTypes.Contains(document.DocumentType, StringComparer.OrdinalIgnoreCase) &&
            _documents.Count(item => !item.IsDeleted && string.Equals(item.DocumentType, document.DocumentType, StringComparison.OrdinalIgnoreCase)) <= 1)
        {
            return Result.Fail(LawyerErrors.RequiredDocumentCannotBeRemoved);
        }

        document.SoftDelete(RequireUtc(nowUtc));
        return Result.Ok();
    }

    public Result SubmitForApproval(Guid changedByUserId, bool accountIsActive, bool profileIsComplete, DateTime nowUtc)
    {
        if (ApprovalStatus is not (LawyerApprovalStatus.Draft or LawyerApprovalStatus.ChangesRequested))
        {
            return Result.Fail(LawyerErrors.InvalidApprovalStatus);
        }

        if (!accountIsActive)
        {
            return Result.Fail(Error.Domain("Account.Inactive", "The account is not active."));
        }

        if (!profileIsComplete)
        {
            return Result.Fail(LawyerErrors.ProfileIncomplete);
        }

        SubmittedOnUtc = RequireUtc(nowUtc);
        ApprovalReason = null;
        TransitionTo(LawyerApprovalStatus.PendingApproval, changedByUserId, null, nowUtc);
        return Result.Ok();
    }

    public Result RequestChanges(Guid changedByUserId, string explanation, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(explanation) || explanation.Trim().Length > 1000)
        {
            return Result.Fail(LawyerErrors.ApprovalReasonRequired);
        }

        if (ApprovalStatus != LawyerApprovalStatus.PendingApproval)
        {
            return Result.Fail(LawyerErrors.InvalidApprovalStatus);
        }

        ApprovalReason = explanation.Trim();
        TransitionTo(LawyerApprovalStatus.ChangesRequested, changedByUserId, ApprovalReason, nowUtc);
        return Result.Ok();
    }

    public Result Approve(Guid changedByUserId, bool profileIsComplete, DateTime nowUtc)
    {
        if (ApprovalStatus != LawyerApprovalStatus.PendingApproval)
        {
            return Result.Fail(LawyerErrors.InvalidApprovalStatus);
        }

        if (!profileIsComplete)
        {
            return Result.Fail(LawyerErrors.ProfileIncomplete);
        }

        ApprovalReason = null;
        ApprovedByUserId = changedByUserId;
        ApprovedOnUtc = RequireUtc(nowUtc);
        SuspensionReason = null;
        SuspendedOnUtc = null;
        TransitionTo(LawyerApprovalStatus.Approved, changedByUserId, null, nowUtc);
        return Result.Ok();
    }

    public Result Reject(Guid changedByUserId, string reason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
        {
            return Result.Fail(LawyerErrors.ApprovalReasonRequired);
        }

        if (ApprovalStatus != LawyerApprovalStatus.PendingApproval)
        {
            return Result.Fail(LawyerErrors.InvalidApprovalStatus);
        }

        ApprovalReason = reason.Trim();
        TransitionTo(LawyerApprovalStatus.Rejected, changedByUserId, ApprovalReason, nowUtc);
        return Result.Ok();
    }

    public Result Suspend(Guid changedByUserId, string reason, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
        {
            return Result.Fail(LawyerErrors.SuspensionReasonRequired);
        }

        if (ApprovalStatus != LawyerApprovalStatus.Approved)
        {
            return Result.Fail(LawyerErrors.InvalidApprovalStatus);
        }

        SuspensionReason = reason.Trim();
        SuspendedOnUtc = RequireUtc(nowUtc);
        TransitionTo(LawyerApprovalStatus.Suspended, changedByUserId, SuspensionReason, nowUtc);
        return Result.Ok();
    }

    public Result Reactivate(Guid changedByUserId, bool profileIsComplete, DateTime nowUtc)
    {
        if (ApprovalStatus != LawyerApprovalStatus.Suspended)
        {
            return Result.Fail(LawyerErrors.InvalidApprovalStatus);
        }

        if (!profileIsComplete)
        {
            return Result.Fail(LawyerErrors.ProfileIncomplete);
        }

        SuspensionReason = null;
        SuspendedOnUtc = null;
        TransitionTo(LawyerApprovalStatus.Approved, changedByUserId, null, nowUtc);
        return Result.Ok();
    }

    public bool CanAppearPublicly(bool accountIsActive, bool profileIsComplete)
        => ApprovalStatus == LawyerApprovalStatus.Approved && accountIsActive && profileIsComplete && !IsDeleted;

    private Result EnsureEditable(Error? error = null)
        => ApprovalStatus is LawyerApprovalStatus.Draft or LawyerApprovalStatus.ChangesRequested or LawyerApprovalStatus.Approved
            ? Result.Ok()
            : Result.Fail(error ?? LawyerErrors.ProfileEditNotAllowed);

    private void TransitionTo(LawyerApprovalStatus newStatus, Guid actorId, string? reason, DateTime nowUtc)
    {
        var previous = ApprovalStatus;
        ApprovalStatus = newStatus;
        _statusHistory.Add(new LawyerApprovalStatusHistory(Id, previous, newStatus, actorId, reason, RequireUtc(nowUtc)));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
