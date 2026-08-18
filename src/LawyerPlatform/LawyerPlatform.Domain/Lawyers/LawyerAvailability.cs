using BuildingBlock.Domain.EntitiesHelper;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Domain.Lawyers;

public sealed class LawyerAvailability : Entity<Guid>
{
    private LawyerAvailability()
    {
    }

    internal LawyerAvailability(
        Guid lawyerConsultationSettingsId,
        ConsultationType consultationType,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime)
        : base(Guid.NewGuid())
    {
        LawyerConsultationSettingsId = lawyerConsultationSettingsId;
        ConsultationType = consultationType;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public Guid LawyerConsultationSettingsId { get; private set; }
    public LawyerConsultationSettings LawyerConsultationSettings { get; private set; } = null!;
    public ConsultationType ConsultationType { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }

    internal void Update(TimeOnly startTime, TimeOnly endTime)
    {
        StartTime = startTime;
        EndTime = endTime;
    }
}
