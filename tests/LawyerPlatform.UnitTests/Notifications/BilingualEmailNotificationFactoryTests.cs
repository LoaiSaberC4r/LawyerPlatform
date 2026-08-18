using BuildingBlock.Application.Time;
using LawyerPlatform.Application.Notifications.Email;
using System.Globalization;

namespace LawyerPlatform.UnitTests.Notifications;

public sealed class BilingualEmailNotificationFactoryTests
{
    private static readonly Guid LawyerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly BilingualEmailNotificationFactory _factory = new(
        new FixedClock(new DateTime(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc)));

    public static TheoryData<EmailNotificationType, EmailNotificationModel, string, string> Templates => new()
    {
        { EmailNotificationType.LawyerSubmittedForApproval, Lawyer(), "Avokatoo | طلب محامٍ جديد للمراجعة | New Lawyer Application", LawyerId.ToString() },
        { EmailNotificationType.LawyerApproved, Lawyer(), "Avokatoo | تم اعتماد حسابك | Your Profile Has Been Approved", "Approved" },
        { EmailNotificationType.LawyerChangesRequested, Lawyer("مراجعة آمنة"), "Avokatoo | مطلوب تعديلات على ملفك | Changes Required", "Changes Requested" },
        { EmailNotificationType.LawyerRejected, Lawyer("سبب آمن"), "Avokatoo | تحديث حالة طلب الاعتماد | Lawyer Application Update", "Rejected" },
        { EmailNotificationType.LawyerSuspended, Lawyer("سبب آمن"), "Avokatoo | تم تعليق حساب المحامي | Lawyer Profile Suspended", "Suspended" },
        { EmailNotificationType.LawyerReactivated, Lawyer(), "Avokatoo | تمت إعادة تفعيل ملفك | Your Lawyer Profile Has Been Reactivated", "Welcome back" },
        { EmailNotificationType.ConsultationRequestCreatedForLawyer, Consultation(), "Avokatoo | طلب استشارة جديد | New Consultation Request", "REF-2026-001" },
        { EmailNotificationType.ConsultationRequestCreatedConfirmation, Consultation(), "Avokatoo | تم استلام طلب الاستشارة | Consultation Request Received", "Current status" },
        { EmailNotificationType.ConsultationUnderReview, Consultation(), "Avokatoo | طلب الاستشارة قيد المراجعة | Consultation Under Review", "Under Review" },
        { EmailNotificationType.ConsultationApproved, Consultation(), "Avokatoo | تمت الموافقة على طلب الاستشارة | Consultation Request Approved", "Approved" },
        { EmailNotificationType.ConsultationRejected, Consultation("سبب آمن"), "Avokatoo | تحديث طلب الاستشارة | Consultation Request Update", "Rejected" },
        { EmailNotificationType.ConsultationCompleted, Consultation(), "Avokatoo | تم إكمال طلب الاستشارة | Consultation Completed", "Completed" },
        { EmailNotificationType.ClientSuspended, Client(), "Avokatoo | تم تعليق حسابك | Your Account Has Been Suspended", "Suspended" },
        { EmailNotificationType.ClientReactivated, Client(), "Avokatoo | تمت إعادة تفعيل حسابك | Your Account Has Been Reactivated", "Active" }
    };

    [Theory]
    [MemberData(nameof(Templates))]
    public void Create_RendersApprovedBilingualTemplate(
        EmailNotificationType notificationType,
        EmailNotificationModel model,
        string expectedSubject,
        string expectedField)
    {
        var result = _factory.Create(notificationType, model);

        Assert.Equal(expectedSubject, result.Subject);
        Assert.Contains("dir=\"rtl\" lang=\"ar\"", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("dir=\"ltr\" lang=\"en\"", result.HtmlBody, StringComparison.Ordinal);
        Assert.True(
            result.HtmlBody.IndexOf("lang=\"ar\"", StringComparison.Ordinal) <
            result.HtmlBody.IndexOf("lang=\"en\"", StringComparison.Ordinal));
        Assert.Contains(expectedField, result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("مرحبًا", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&copy; 2026", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Avokatoo", result.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Platform", result.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Platform", result.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_HtmlEncodesAllArbitraryBusinessText()
    {
        const string unsafeText = "<script>alert('x')</script> & injected";

        var lawyer = _factory.Create(
            EmailNotificationType.LawyerChangesRequested,
            new LawyerEmailNotificationModel(unsafeText, LawyerId, unsafeText));
        var consultation = _factory.Create(
            EmailNotificationType.ConsultationRejected,
            new ConsultationEmailNotificationModel(
                unsafeText,
                unsafeText,
                unsafeText,
                unsafeText,
                unsafeText,
                Reason: unsafeText));

        Assert.DoesNotContain("<script>", lawyer.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script>", consultation.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", lawyer.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", consultation.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("&amp; injected", consultation.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsultationTemplates_NeverIncludeDescription_AndOmitMissingPreferredAppointment()
    {
        const string privateDescription = "PRIVATE-CONSULTATION-DESCRIPTION";
        var model = Consultation("safe reason");

        foreach (var type in Enum.GetValues<EmailNotificationType>()
                     .Where(type => type is >= EmailNotificationType.ConsultationRequestCreatedForLawyer
                         and <= EmailNotificationType.ConsultationCompleted))
        {
            var body = _factory.Create(type, model).HtmlBody;
            Assert.DoesNotContain(privateDescription, body, StringComparison.Ordinal);
            Assert.DoesNotContain("Preferred consultation date:", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Recorded preferred date:", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ConsultationTemplates_RenderPreferredAppointmentOnlyWhenPresent()
    {
        var model = Consultation() with
        {
            PreferredAppointmentOnUtc = new DateTime(2026, 9, 1, 12, 30, 0, DateTimeKind.Utc)
        };

        var lawyerBody = _factory.Create(
            EmailNotificationType.ConsultationRequestCreatedForLawyer,
            model).HtmlBody;
        var approvedBody = _factory.Create(
            EmailNotificationType.ConsultationApproved,
            model).HtmlBody;

        Assert.Contains("Preferred consultation date:", lawyerBody, StringComparison.Ordinal);
        Assert.Contains("Recorded preferred date:", approvedBody, StringComparison.Ordinal);
        Assert.Contains("2026-09-01 12:30 UTC", approvedBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsultationApproved_RendersSafeClickableMapLocationOnlyWhenPresent()
    {
        const string mapUrl =
            "https://www.google.com/maps/search/?api=1&query=30.044420,31.235712";
        var withMap = Consultation() with { LawyerOfficeMapUrl = mapUrl };

        var approved = _factory.Create(EmailNotificationType.ConsultationApproved, withMap);
        var withoutMap = _factory.Create(EmailNotificationType.ConsultationApproved, Consultation());
        var created = _factory.Create(
            EmailNotificationType.ConsultationRequestCreatedConfirmation,
            withMap);

        Assert.Contains("موقع مكتب المحامي", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("عرض موقع المكتب على Google Maps", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Lawyer Office Location", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("View Lawyer Office on Google Maps", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains($"href=\"{mapUrl.Replace("&", "&amp;", StringComparison.Ordinal)}\"", approved.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Office Location", withoutMap.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("موقع مكتب المحامي", withoutMap.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain(mapUrl, created.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Office Location", created.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsultationApproved_DoesNotRenderUntrustedMapUrl()
    {
        var model = Consultation() with { LawyerOfficeMapUrl = "javascript:alert('x')" };

        var result = _factory.Create(EmailNotificationType.ConsultationApproved, model);

        Assert.DoesNotContain("javascript:", result.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lawyer Office Location", result.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void LawyerOfficeMapUrlBuilder_UsesInvariantCultureAndRequiresBothCoordinates()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.Equal(
                "https://www.google.com/maps/search/?api=1&query=30.044420,31.235712",
                LawyerOfficeMapUrlBuilder.Create(30.044420m, 31.235712m));
            Assert.Null(LawyerOfficeMapUrlBuilder.Create(30.044420m, null));
            Assert.Null(LawyerOfficeMapUrlBuilder.Create(null, 31.235712m));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Create_RejectsUnknownTypeAndIncompatibleModel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _factory.Create((EmailNotificationType)999, Client()));
        Assert.Throws<ArgumentException>(() =>
            _factory.Create(EmailNotificationType.LawyerApproved, Client()));
    }

    private static LawyerEmailNotificationModel Lawyer(string? reason = null)
        => new("المحامي Lawyer", LawyerId, reason);

    private static ConsultationEmailNotificationModel Consultation(string? reason = null)
        => new(
            "مقدم الطلب Requester",
            "المحامي Lawyer",
            "REF-2026-001",
            "قانون مدني",
            "Civil Law",
            Reason: reason);

    private static ClientEmailNotificationModel Client()
        => new("العميل Client");

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
