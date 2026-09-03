using System.Security.Cryptography;
using LawyerPlatform.Application.Abstractions.Consultations;

namespace LawyerPlatform.Infrastructure.Consultations;

internal sealed class ConsultationReferenceNumberGenerator : IConsultationReferenceNumberGenerator
{
    private const string AllowedCharacters = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const int SuffixLength = 6;

    public string Generate()
    {
        return string.Create(3 + SuffixLength, 0, static (value, _) =>
        {
            value[0] = 'A';
            value[1] = 'V';
            value[2] = '-';
            for (var index = 0; index < SuffixLength; index++)
            {
                value[index + 3] = AllowedCharacters[
                    RandomNumberGenerator.GetInt32(AllowedCharacters.Length)];
            }
        });
    }
}
