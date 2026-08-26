namespace LawyerPlatform.Application.Abstractions.Authentication;

public interface IPasswordResetTokenHasher
{
    string HashOtp(string otp);
    bool VerifyOtp(string otp, string otpHash);
    string HashResetToken(string resetToken);
    bool VerifyResetToken(string resetToken, string resetTokenHash);
}
