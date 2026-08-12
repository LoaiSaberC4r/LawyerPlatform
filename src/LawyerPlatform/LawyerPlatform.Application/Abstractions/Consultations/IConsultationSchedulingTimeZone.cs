namespace LawyerPlatform.Application.Abstractions.Consultations;

public interface IConsultationSchedulingTimeZone
{
    DateTime ConvertUtcToBusinessLocal(DateTime utcDateTime);
}
