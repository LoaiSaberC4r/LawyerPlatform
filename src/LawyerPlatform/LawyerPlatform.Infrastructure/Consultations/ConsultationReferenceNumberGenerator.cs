using System.Security.Cryptography;
using LawyerPlatform.Application.Abstractions.Consultations;

namespace LawyerPlatform.Infrastructure.Consultations;

internal sealed class ConsultationReferenceNumberGenerator : IConsultationReferenceNumberGenerator
{
    public string Generate()
        => $"CR-{Convert.ToHexString(RandomNumberGenerator.GetBytes(10))}";
}
