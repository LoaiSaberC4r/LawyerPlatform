using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Lawyers;
using LawyerPlatform.Domain.ReferenceData;

namespace LawyerPlatform.Domain.Consultations;

public sealed class ConsultationRequest : AggregateRoot<Guid>, IAuditableEntity
{
    public const int MaximumDescriptionLength = 4000;
    public const int MaximumReferenceNumberLength = 32;
    private readonly List<ConsultationRequestStatusHistory> _statusHistory = [];

    private ConsultationRequest()
    {
    }

    private ConsultationRequest(
        string referenceNumber,
        Guid? clientProfileId,
        string? guestFullName,
        string? guestPhoneNumber,
        string? guestEmail,
        Guid lawyerProfileId,
        ConsultationType consultationType,
        int? legalSpecializationId,
        string description,
        DateTime? preferredAppointmentOnUtc,
        DateTime createdOnUtc,
        decimal? consultationPrice)
        : base(Guid.NewGuid())
    {
        ReferenceNumber = referenceNumber.Trim();
        ClientProfileId = clientProfileId;
        GuestFullName = Normalize(guestFullName);
        GuestPhoneNumber = Normalize(guestPhoneNumber);
        GuestEmail = Normalize(guestEmail);
        LawyerProfileId = lawyerProfileId;
        ConsultationType = consultationType;
        LegalSpecializationId = legalSpecializationId;
        Description = description.Trim();
        PreferredAppointmentOnUtc = preferredAppointmentOnUtc is { } preferred
            ? RequireUtc(preferred)
            : null;
        ConsultationPrice = consultationPrice;
        Status = ConsultationRequestStatus.New;
        CreatedOnUtc = RequireUtc(createdOnUtc);
    }

    public string ReferenceNumber { get; private set; } = string.Empty;
    public Guid? ClientProfileId { get; private set; }
    public ClientProfile? ClientProfile { get; private set; }
    public string? GuestFullName { get; private set; }
    public string? GuestPhoneNumber { get; private set; }
    public string? GuestEmail { get; private set; }
    public Guid LawyerProfileId { get; private set; }
    public LawyerProfile LawyerProfile { get; private set; } = null!;
    public ConsultationType ConsultationType { get; private set; }
    public int? LegalSpecializationId { get; private set; }
    public LegalSpecialization? LegalSpecialization { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime? PreferredAppointmentOnUtc { get; private set; }
    public decimal? ConsultationPrice { get; private set; }
    public ConsultationRequestStatus Status { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public DateTime? CompletedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<ConsultationRequestStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public static Result<ConsultationRequest> CreateForGuest(
        string referenceNumber,
        string fullName,
        string phoneNumber,
        string? email,
        Guid lawyerProfileId,
        ConsultationType consultationType,
        int? legalSpecializationId,
        string description,
        DateTime? preferredAppointmentOnUtc,
        DateTime createdOnUtc,
        decimal? consultationPrice = null)
        => Create(
            referenceNumber,
            null,
            fullName,
            phoneNumber,
            email,
            lawyerProfileId,
            consultationType,
            legalSpecializationId,
            description,
            preferredAppointmentOnUtc,
            createdOnUtc,
            consultationPrice);

    public static Result<ConsultationRequest> CreateForClient(
        string referenceNumber,
        Guid clientProfileId,
        Guid lawyerProfileId,
        ConsultationType consultationType,
        int? legalSpecializationId,
        string description,
        DateTime? preferredAppointmentOnUtc,
        DateTime createdOnUtc,
        decimal? consultationPrice = null)
        => Create(
            referenceNumber,
            clientProfileId,
            null,
            null,
            null,
            lawyerProfileId,
            consultationType,
            legalSpecializationId,
            description,
            preferredAppointmentOnUtc,
            createdOnUtc,
            consultationPrice);

    public Result ChangeStatus(
        ConsultationRequestStatus newStatus,
        Guid changedByUserId,
        string? reason,
        DateTime changedOnUtc)
    {
        if (changedByUserId == Guid.Empty || !IsAllowedTransition(Status, newStatus))
        {
            return Result.Fail(ConsultationRequestErrors.InvalidStatusTransition);
        }

        var normalizedReason = Normalize(reason);
        if (newStatus == ConsultationRequestStatus.Rejected &&
            (normalizedReason is null || normalizedReason.Length > 1000))
        {
            return Result.Fail(ConsultationRequestErrors.RejectionReasonRequired);
        }

        var changedOn = RequireUtc(changedOnUtc);
        var oldStatus = Status;
        Status = newStatus;
        if (newStatus == ConsultationRequestStatus.Completed)
        {
            CompletedOnUtc = changedOn;
        }

        _statusHistory.Add(new ConsultationRequestStatusHistory(
            Id,
            oldStatus,
            newStatus,
            changedByUserId,
            newStatus == ConsultationRequestStatus.Rejected ? normalizedReason : null,
            changedOn));
        return Result.Ok();
    }

    public static Result<ConsultationRequest> Create(
        string referenceNumber,
        Guid? clientProfileId,
        string? guestFullName,
        string? guestPhoneNumber,
        string? guestEmail,
        Guid lawyerProfileId,
        ConsultationType consultationType,
        int? legalSpecializationId,
        string description,
        DateTime? preferredAppointmentOnUtc,
        DateTime createdOnUtc,
        decimal? consultationPrice = null)
    {
        var hasClientSource = clientProfileId is { } clientId && clientId != Guid.Empty;
        var hasGuestSource = !string.IsNullOrWhiteSpace(guestFullName) &&
                             !string.IsNullOrWhiteSpace(guestPhoneNumber);
        var guestFieldsAreEmpty = string.IsNullOrWhiteSpace(guestFullName) &&
                                  string.IsNullOrWhiteSpace(guestPhoneNumber) &&
                                  string.IsNullOrWhiteSpace(guestEmail);
        if (hasClientSource == hasGuestSource || hasClientSource && !guestFieldsAreEmpty)
        {
            return Result<ConsultationRequest>.Fail(ConsultationRequestErrors.InvalidSource);
        }

        if (!Enum.IsDefined(consultationType))
        {
            return Result<ConsultationRequest>.Fail(ConsultationRequestErrors.InvalidConsultationType);
        }

        if (consultationType == ConsultationType.Online && !consultationPrice.HasValue)
        {
            return Result<ConsultationRequest>.Fail(ConsultationRequestErrors.ConsultationPriceRequired);
        }

        if (consultationType == ConsultationType.Onsite && consultationPrice.HasValue)
        {
            return Result<ConsultationRequest>.Fail(ConsultationRequestErrors.OnsitePriceNotAllowed);
        }

        if (string.IsNullOrWhiteSpace(referenceNumber) ||
            referenceNumber.Trim().Length > MaximumReferenceNumberLength ||
            lawyerProfileId == Guid.Empty ||
            legalSpecializationId <= 0 ||
            string.IsNullOrWhiteSpace(description) ||
            description.Trim().Length > MaximumDescriptionLength ||
            guestFullName?.Trim().Length > 200 ||
            guestPhoneNumber?.Trim().Length > 30 ||
            guestEmail?.Trim().Length > 320 ||
            consultationPrice is <= 0 ||
            consultationPrice > LawyerConsultationSettings.MaximumConsultationPrice ||
            consultationPrice.HasValue && decimal.Round(consultationPrice.Value, 2) != consultationPrice.Value)
        {
            return Result<ConsultationRequest>.Fail(ConsultationRequestErrors.Invalid);
        }

        return Result<ConsultationRequest>.Ok(new ConsultationRequest(
            referenceNumber,
            clientProfileId,
            guestFullName,
            guestPhoneNumber,
            guestEmail,
            lawyerProfileId,
            consultationType,
            legalSpecializationId,
            description,
            preferredAppointmentOnUtc,
            createdOnUtc,
            consultationPrice));
    }

    private static bool IsAllowedTransition(
        ConsultationRequestStatus oldStatus,
        ConsultationRequestStatus newStatus)
        => (oldStatus, newStatus) switch
        {
            (ConsultationRequestStatus.New, ConsultationRequestStatus.UnderReview or ConsultationRequestStatus.Approved or ConsultationRequestStatus.Rejected) => true,
            (ConsultationRequestStatus.UnderReview, ConsultationRequestStatus.Approved or ConsultationRequestStatus.Rejected) => true,
            (ConsultationRequestStatus.Approved, ConsultationRequestStatus.Completed) => true,
            _ => false
        };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
