using System.Globalization;
using System.Net;
using System.Text;
using BuildingBlock.Application.Email;
using BuildingBlock.Application.Time;

namespace LawyerPlatform.Application.Notifications.Email;

internal sealed class PasswordResetOtpEmailFactory(
    IDateTimeProvider clock,
    IEmailBrandingProvider emailBranding)
    : IPasswordResetOtpEmailFactory
{
    public EmailMessage Create(string recipientEmail, string otp, int expirationMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(otp);

        var safeOtp = WebUtility.HtmlEncode(otp);
        var safeFooterUrl = WebUtility.HtmlEncode(emailBranding.FooterImageUrl);
        var expiration = expirationMinutes.ToString(CultureInfo.InvariantCulture);
        var html = new StringBuilder(2048)
            .Append("<!doctype html><html><head><meta charset=\"utf-8\"></head>")
            .Append("<body style=\"font-family:Arial,sans-serif;color:#222;line-height:1.6\">")
            .Append("<div style=\"max-width:680px;margin:0 auto\"><h2>Avokatoo</h2>")
            .Append("<section dir=\"rtl\" lang=\"ar\" style=\"text-align:right\">")
            .Append("<p>لقد تلقينا طلبًا لإعادة تعيين كلمة المرور الخاصة بحسابك على Avokatoo.</p>")
            .Append("<p>رمز التحقق:<br><strong style=\"font-size:28px;letter-spacing:6px\">")
            .Append(safeOtp)
            .Append("</strong></p><p>صلاحية الرمز: ")
            .Append(expiration)
            .Append(" دقائق</p><p>إذا لم تطلب إعادة تعيين كلمة المرور، يمكنك تجاهل هذه الرسالة.</p>")
            .Append("</section><hr style=\"border:0;border-top:1px solid #bbb;margin:28px 0\">")
            .Append("<section dir=\"ltr\" lang=\"en\" style=\"text-align:left\">")
            .Append("<p>We received a request to reset your Avokatoo account password.</p>")
            .Append("<p>Verification code:<br><strong style=\"font-size:28px;letter-spacing:6px\">")
            .Append(safeOtp)
            .Append("</strong></p><p>This code expires in ")
            .Append(expiration)
            .Append(" minutes.</p><p>If you did not request a password reset, you can safely ignore this email.</p>")
            .Append("</section><footer style=\"margin-top:28px;color:#666\">Avokatoo<br>&copy; ")
            .Append(clock.UtcNow.Year)
            .Append("</footer><table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin-top:24px;\">")
            .Append("<tr><td align=\"center\"><img src=\"")
            .Append(safeFooterUrl)
            .Append("\" alt=\"Avokatoo\" width=\"700\" style=\"display:block;width:100%;max-width:700px;height:auto;border:0\"></td></tr>")
            .Append("</table></div></body></html>")
            .ToString();

        return new EmailMessage
        {
            To = [recipientEmail],
            Subject = "Avokatoo | رمز إعادة تعيين كلمة المرور | Password Reset Code",
            HtmlBody = html
        };
    }
}
