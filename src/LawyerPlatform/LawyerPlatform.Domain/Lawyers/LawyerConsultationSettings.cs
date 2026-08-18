using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Domain.Lawyers;

public sealed record LawyerAvailabilityPeriod(
    ConsultationType ConsultationType,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime);

public sealed class LawyerConsultationSettings : AggregateRoot<Guid>, IAuditableEntity
{
    public const decimal MaximumConsultationPrice = 9_999_999_999_999_999.99m;
    private readonly List<LawyerAvailability> _availability = [];

    private LawyerConsultationSettings()
    {
    }

    private LawyerConsultationSettings(
        Guid lawyerProfileId,
        decimal onlineConsultationPrice,
        IReadOnlyCollection<LawyerAvailabilityPeriod> availability,
        DateTime createdOnUtc)
        : base(Guid.NewGuid())
    {
        LawyerProfileId = lawyerProfileId;
        OnlineConsultationPrice = onlineConsultationPrice;
        CreatedOnUtc = RequireUtc(createdOnUtc);
        ReplaceAvailabilityInternal(availability);
    }

    public Guid LawyerProfileId { get; private set; }
    public LawyerProfile LawyerProfile { get; private set; } = null!;
    public decimal OnlineConsultationPrice { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<LawyerAvailability> Availability => _availability.AsReadOnly();

    public static Result<LawyerConsultationSettings> Create(
        Guid lawyerProfileId,
        decimal onlineConsultationPrice,
        IReadOnlyCollection<LawyerAvailabilityPeriod> availability,
        DateTime createdOnUtc)
    {
        ArgumentNullException.ThrowIfNull(availability);

        var validation = Validate(lawyerProfileId, onlineConsultationPrice, availability);
        return validation.IsFailure
            ? Result<LawyerConsultationSettings>.Fail(validation.Errors)
            : Result<LawyerConsultationSettings>.Ok(new LawyerConsultationSettings(
                lawyerProfileId,
                onlineConsultationPrice,
                availability,
                createdOnUtc));
    }

    public Result Update(
        decimal onlineConsultationPrice,
        IReadOnlyCollection<LawyerAvailabilityPeriod> availability)
    {
        ArgumentNullException.ThrowIfNull(availability);

        var validation = Validate(LawyerProfileId, onlineConsultationPrice, availability);
        if (validation.IsFailure)
        {
            return validation;
        }

        OnlineConsultationPrice = onlineConsultationPrice;
        ReplaceAvailabilityInternal(availability);
        return Result.Ok();
    }

    private static Result Validate(
        Guid lawyerProfileId,
        decimal onlineConsultationPrice,
        IReadOnlyCollection<LawyerAvailabilityPeriod> availability)
    {
        if (lawyerProfileId == Guid.Empty)
        {
            return Result.Fail(LawyerErrors.NotFound);
        }

        if (onlineConsultationPrice <= 0 ||
            onlineConsultationPrice > MaximumConsultationPrice ||
            decimal.Round(onlineConsultationPrice, 2) != onlineConsultationPrice)
        {
            return Result.Fail(LawyerErrors.ConsultationPriceInvalid);
        }

        if (availability.Any(period =>
                !Enum.IsDefined(period.ConsultationType) ||
                !Enum.IsDefined(period.DayOfWeek) ||
                period.StartTime >= period.EndTime) ||
            availability.GroupBy(period => period.ConsultationType).Any(group => group.Count() > 7))
        {
            return Result.Fail(LawyerErrors.AvailabilityInvalid);
        }

        if (availability
            .GroupBy(period => new { period.ConsultationType, period.DayOfWeek })
            .Any(group => group.Count() > 1))
        {
            return Result.Fail(LawyerErrors.DuplicateAvailabilityDay);
        }

        return Result.Ok();
    }

    private void ReplaceAvailabilityInternal(IReadOnlyCollection<LawyerAvailabilityPeriod> availability)
    {
        var submittedPeriods = availability
            .Select(period => (period.ConsultationType, period.DayOfWeek))
            .ToHashSet();
        _availability.RemoveAll(item => !submittedPeriods.Contains((item.ConsultationType, item.DayOfWeek)));

        foreach (var period in availability)
        {
            var existing = _availability.SingleOrDefault(item =>
                item.ConsultationType == period.ConsultationType &&
                item.DayOfWeek == period.DayOfWeek);
            if (existing is null)
            {
                _availability.Add(new LawyerAvailability(
                    Id,
                    period.ConsultationType,
                    period.DayOfWeek,
                    period.StartTime,
                    period.EndTime));
            }
            else
            {
                existing.Update(period.StartTime, period.EndTime);
            }
        }
    }

    private static DateTime RequireUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
