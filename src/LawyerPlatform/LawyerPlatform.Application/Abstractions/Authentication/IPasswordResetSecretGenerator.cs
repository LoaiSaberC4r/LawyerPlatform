namespace LawyerPlatform.Application.Abstractions.Authentication;

public interface IPasswordResetSecretGenerator
{
    string GenerateOtp();
    string GenerateResetToken();
}
