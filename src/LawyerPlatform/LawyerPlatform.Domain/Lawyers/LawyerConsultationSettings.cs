using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace LawyerPlatform.Domain.Lawyers;

public sealed record LawyerAvailabilityPeriod(
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
        decimal consultationPrice,
        IReadOnlyCollection<LawyerAvailabilityPeriod> availability,
        DateTime createdOnUtc)
        : base(Guid.NewGuid())
    {
        LawyerProfileId = lawyerProfileId;
        ConsultationPrice = consultationPrice;
        CreatedOnUtc = RequireUtc(createdOnUtc);
        ReplaceAvailabilityInternal(availability);
    }

    public Guid LawyerProfileId { get; private set; }
    public LawyerProfile LawyerProfile { get; private set; } = null!;
    public decimal ConsultationPrice { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<LawyerAvailability> Availability => _availability.AsReadOnly();

    public static Result<LawyerConsultationSettings> Create(
        Guid lawyerProfileId,
        decimal consultationPrice,
        IReadOnlyCollection<LawyerAvailabilityPeriod> availability,
        DateTime createdOnUtc)
    {
        ArgumentNullException.ThrowIfNull(availability);

        var validation = Validate(lawyerProfileId, consultationPrice, availability);
        return validation.IsFailure
            ? Result<LawyerConsultationSettings>.Fail(validation.Errors)
            : Result<LawyerConsultationSettings>.Ok(new LawyerConsultationSettings(
                lawyerProfileId,
                consultationPrice,
                availability,
                createdOnUtc));
    }

    public Result Update(
        decimal consultationPrice,
        IReadOnlyCollection<LawyerAvailabilityPeriod> availability)
    {
        ArgumentNullException.ThrowIfNull(availability);

        var validation = Validate(LawyerProfileId, consultationPrice, availability);
        if (validation.IsFailure)
        {
            return validation;
        }

        ConsultationPrice = consultationPrice;
        ReplaceAvailabilityInternal(availability);
        return Result.Ok();
    }

    private static Result Validate(
        Guid lawyerProfileId,
        decimal consultationPrice,
        IReadOnlyCollection<LawyerAvailabilityPeriod> availability)
    {
        if (lawyerProfileId == Guid.Empty)
        {
            return Result.Fail(LawyerErrors.NotFound);
        }

        if (consultationPrice <= 0 ||
            consultationPrice > MaximumConsultationPrice ||
            decimal.Round(consultationPrice, 2) != consultationPrice)
        {
            return Result.Fail(LawyerErrors.ConsultationPriceInvalid);
        }

        if (availability.Count > 7 || availability.Any(period =>
                !Enum.IsDefined(period.DayOfWeek) || period.StartTime >= period.EndTime))
        {
            return Result.Fail(LawyerErrors.AvailabilityInvalid);
        }

        if (availability.Select(period => period.DayOfWeek).Distinct().Count() != availability.Count)
        {
            return Result.Fail(LawyerErrors.DuplicateAvailabilityDay);
        }

        return Result.Ok();
    }

    private void ReplaceAvailabilityInternal(IReadOnlyCollection<LawyerAvailabilityPeriod> availability)
    {
        var submittedDays = availability.Select(period => period.DayOfWeek).ToHashSet();
        _availability.RemoveAll(item => !submittedDays.Contains(item.DayOfWeek));

        foreach (var period in availability)
        {
            var existing = _availability.SingleOrDefault(item => item.DayOfWeek == period.DayOfWeek);
            if (existing is null)
            {
                _availability.Add(new LawyerAvailability(
                    Id,
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
