using BuildingBlock.Application.Email;

namespace LawyerPlatform.Application.Notifications.Email;

public interface IPasswordResetOtpEmailFactory
{
    EmailMessage Create(string recipientEmail, string otp, int expirationMinutes);
}
