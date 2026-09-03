using BuildingBlock.Application.Time;
using LawyerPlatform.Application.Notifications.Email;
using System.Globalization;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.UnitTests.Notifications;

public sealed class BilingualEmailNotificationFactoryTests
{
    private const string FooterImageUrl =
        "https://cdn.example.test/email-assets/avokatoo-email-footer.png";
    private const string TrackingUrl =
        "https://frontend.example.test/consultation/track";
    private static readonly Guid LawyerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly BilingualEmailNotificationFactory _factory = new(
        new FixedClock(new DateTime(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc)),
        new FixedEmailBrandingProvider(FooterImageUrl));

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
        { EmailNotificationType.ClientReactivated, Client(), "Avokatoo | تمت إعادة تفعيل حسابك | Your Account Has Been Reactivated", "Active" },
        { EmailNotificationType.LawyerRegistrationWelcome, Registration(), "Avokatoo | مرحبًا بك كمحامٍ | Welcome to Avokatoo", "Draft" },
        { EmailNotificationType.ClientRegistrationWelcome, Registration(), "Avokatoo | مرحبًا بك | Welcome to Avokatoo", "searching for Lawyers" },
        { EmailNotificationType.ContactInquirySupportNotification, ContactInquiry(), "Avokatoo | استفسار تواصل جديد | New Contact Inquiry", "Technical Support" },
        { EmailNotificationType.ContactInquiryConfirmation, ContactInquiry(), "Avokatoo | تم استلام استفسارك | Inquiry Received", "successfully received your inquiry" }
    };

    [Fact]
    public void EmailNotificationType_PreservesPersistedNumericValues()
    {
        Assert.Equal(1, (int)EmailNotificationType.LawyerSubmittedForApproval);
        Assert.Equal(2, (int)EmailNotificationType.LawyerApproved);
        Assert.Equal(3, (int)EmailNotificationType.LawyerChangesRequested);
        Assert.Equal(4, (int)EmailNotificationType.LawyerRejected);
        Assert.Equal(5, (int)EmailNotificationType.LawyerSuspended);
        Assert.Equal(6, (int)EmailNotificationType.LawyerReactivated);
        Assert.Equal(7, (int)EmailNotificationType.ConsultationRequestCreatedForLawyer);
        Assert.Equal(8, (int)EmailNotificationType.ConsultationRequestCreatedConfirmation);
        Assert.Equal(9, (int)EmailNotificationType.ConsultationUnderReview);
        Assert.Equal(10, (int)EmailNotificationType.ConsultationApproved);
        Assert.Equal(11, (int)EmailNotificationType.ConsultationRejected);
        Assert.Equal(12, (int)EmailNotificationType.ConsultationCompleted);
        Assert.Equal(13, (int)EmailNotificationType.ClientSuspended);
        Assert.Equal(14, (int)EmailNotificationType.ClientReactivated);
        Assert.Equal(15, (int)EmailNotificationType.LawyerRegistrationWelcome);
        Assert.Equal(16, (int)EmailNotificationType.ClientRegistrationWelcome);
        Assert.Equal(17, (int)EmailNotificationType.ContactInquirySupportNotification);
        Assert.Equal(18, (int)EmailNotificationType.ContactInquiryConfirmation);
    }

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

    [Theory]
    [MemberData(nameof(Templates))]
    public void Create_AppendsOneGlobalFooterAfterBothLanguageSections(
        EmailNotificationType notificationType,
        EmailNotificationModel model,
        string expectedSubject,
        string expectedField)
    {
        var result = _factory.Create(notificationType, model);
        var body = result.HtmlBody;

        var arabicIndex = body.IndexOf("lang=\"ar\"", StringComparison.Ordinal);
        var englishIndex = body.IndexOf("lang=\"en\"", StringComparison.Ordinal);
        var footerImageIndex = body.IndexOf(FooterImageUrl, StringComparison.Ordinal);

        Assert.Equal(Enum.GetValues<EmailNotificationType>().Length, Templates.Count);
        Assert.Equal(expectedSubject, result.Subject);
        Assert.Contains(expectedField, body, StringComparison.Ordinal);
        Assert.True(arabicIndex >= 0);
        Assert.True(englishIndex > arabicIndex);
        Assert.True(footerImageIndex > englishIndex);
        Assert.Equal(1, CountOccurrences(body, FooterImageUrl));
        Assert.Equal(1, CountOccurrences(body, "alt=\"Avokatoo\""));
        Assert.Contains(
            "width=\"700\" style=\"display:block;width:100%;max-width:700px;height:auto;",
            body,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Create_HtmlEncodesConfiguredFooterImageUrl()
    {
        const string urlWithHtmlSpecialCharacter =
            "https://cdn.example.test/email-assets/avokatoo&wide.png";
        var factory = new BilingualEmailNotificationFactory(
            new FixedClock(new DateTime(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc)),
            new FixedEmailBrandingProvider(urlWithHtmlSpecialCharacter));

        var body = factory.Create(EmailNotificationType.ClientReactivated, Client()).HtmlBody;

        Assert.Contains(
            "src=\"https://cdn.example.test/email-assets/avokatoo&amp;wide.png\"",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain($"src=\"{urlWithHtmlSpecialCharacter}\"", body, StringComparison.Ordinal);
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
    public void LawyerRegistrationWelcome_ExplainsDraftOnboardingAndApprovalBeforeVisibility()
    {
        var result = _factory.Create(
            EmailNotificationType.LawyerRegistrationWelcome,
            Registration());

        Assert.Contains("المستخدم Registrant", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("registrant@example.test", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("registrant.user", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("مسودة", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Draft", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Complete your professional profile", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("office information and location", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("legal specializations", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("required professional documents", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Submit For Approval", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("administration approves it", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("will not appear in public Lawyer search", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(FooterImageUrl, result.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientRegistrationWelcome_ExplainsClientServicesWithoutLawyerOnboarding()
    {
        var result = _factory.Create(
            EmailNotificationType.ClientRegistrationWelcome,
            Registration());

        Assert.Contains("المستخدم Registrant", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("registrant@example.test", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("registrant.user", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Welcome to Avokatoo", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("searching for Lawyers", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("consultation requests", result.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Submit For Approval", result.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("professional documents", result.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("administration approves it", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(FooterImageUrl, result.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationWelcome_HtmlEncodesIdentityValuesAndCannotContainAPassword()
    {
        const string unsafeFullName = "<script>alert(1)</script>";
        const string unsafeEmail = "mail+test<&>\"'@example.test";
        const string unsafeUserName = "user<&>\"'";
        const string password = "RegistrationPassword1";
        var model = new RegistrationWelcomeEmailNotificationModel(
            unsafeFullName,
            unsafeEmail,
            unsafeUserName);

        foreach (var type in new[]
                 {
                     EmailNotificationType.LawyerRegistrationWelcome,
                     EmailNotificationType.ClientRegistrationWelcome
                 })
        {
            var result = _factory.Create(type, model);

            Assert.DoesNotContain("<script>", result.HtmlBody, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(unsafeEmail, result.HtmlBody, StringComparison.Ordinal);
            Assert.DoesNotContain(unsafeUserName, result.HtmlBody, StringComparison.Ordinal);
            Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", result.HtmlBody, StringComparison.Ordinal);
            Assert.Contains("mail+test&lt;&amp;&gt;&quot;&#39;@example.test", result.HtmlBody, StringComparison.Ordinal);
            Assert.Contains("user&lt;&amp;&gt;&quot;&#39;", result.HtmlBody, StringComparison.Ordinal);
            Assert.DoesNotContain(password, result.HtmlBody, StringComparison.Ordinal);
            Assert.DoesNotContain("PasswordHash", result.HtmlBody, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            typeof(RegistrationWelcomeEmailNotificationModel).GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ContactInquiryTemplates_RenderRequiredBilingualContentAndEncodeAllUserInput()
    {
        const string unsafeText = "<script>alert('x')</script> & value";
        var model = new ContactInquiryEmailNotificationModel(
            unsafeText,
            unsafeText,
            "unsafe@example.test",
            unsafeText,
            $"first line\n{unsafeText}",
            new DateTime(2026, 8, 30, 12, 30, 0, DateTimeKind.Utc));

        var support = _factory.Create(
            EmailNotificationType.ContactInquirySupportNotification,
            model);
        var confirmation = _factory.Create(
            EmailNotificationType.ContactInquiryConfirmation,
            model);

        Assert.Contains("Full Name:", support.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Phone Number:", support.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Email:", support.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Inquiry Type:", support.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Message / Details:", support.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("first line<br>&lt;script&gt;", support.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", support.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script>", confirmation.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(unsafeText, support.Subject, StringComparison.Ordinal);
        Assert.Contains("تم استلام استفسارك بنجاح", confirmation.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("successfully received your inquiry", confirmation.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(FooterImageUrl, support.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(FooterImageUrl, confirmation.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Platform", support.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Platform", confirmation.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_SharedLayoutUsesLargerTypographyAndPreservesBilingualBranding()
    {
        var result = _factory.Create(
            EmailNotificationType.ClientRegistrationWelcome,
            Registration());

        Assert.Contains("font-size:20px", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("font-size:28px", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("line-height:1.7", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("dir=\"rtl\"", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("dir=\"ltr\"", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(FooterImageUrl, result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Avokatoo", result.HtmlBody, StringComparison.Ordinal);
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
    public void ConsultationTemplatesRenderTypeAndOnlinePriceOnly()
    {
        var online = Consultation() with
        {
            ConsultationType = ConsultationType.Online,
            ConsultationPrice = 500m
        };
        var onsite = Consultation() with
        {
            ConsultationType = ConsultationType.Onsite,
            ConsultationPrice = null
        };

        var onlineBody = _factory.Create(
            EmailNotificationType.ConsultationRequestCreatedConfirmation,
            online).HtmlBody;
        var onsiteBody = _factory.Create(
            EmailNotificationType.ConsultationRequestCreatedConfirmation,
            onsite).HtmlBody;

        Assert.Contains("Consultation type:<br>Online", onlineBody, StringComparison.Ordinal);
        Assert.Contains("Consultation price:<br>500.00 EGP", onlineBody, StringComparison.Ordinal);
        Assert.Contains("Consultation type:<br>Onsite", onsiteBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Consultation price:", onsiteBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsultationApproved_RendersSafeClickableMapLocationOnlyWhenPresent()
    {
        const string publicPhoneNumber = "01012345678";
        const string mapUrl =
            "https://www.google.com/maps/search/?api=1&query=30.044420,31.235712";
        var withPhoneAndMap = Consultation() with
        {
            LawyerPublicPhoneNumber = publicPhoneNumber,
            LawyerOfficeMapUrl = mapUrl
        };
        var withPhoneOnly = Consultation() with { LawyerPublicPhoneNumber = publicPhoneNumber };
        var withMapOnly = Consultation() with { LawyerOfficeMapUrl = mapUrl };

        var approved = _factory.Create(EmailNotificationType.ConsultationApproved, withPhoneAndMap);
        var phoneOnly = _factory.Create(EmailNotificationType.ConsultationApproved, withPhoneOnly);
        var mapOnly = _factory.Create(EmailNotificationType.ConsultationApproved, withMapOnly);
        var withoutContact = _factory.Create(EmailNotificationType.ConsultationApproved, Consultation());

        Assert.Contains("رقم هاتف المحامي:", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Lawyer Phone Number:", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(publicPhoneNumber, approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("موقع مكتب المحامي", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("عرض موقع المكتب على Google Maps", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Lawyer Office Location", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("View Lawyer Office on Google Maps", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains($"href=\"{mapUrl.Replace("&", "&amp;", StringComparison.Ordinal)}\"", approved.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(publicPhoneNumber, phoneOnly.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Office Location", phoneOnly.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("google.com/maps", phoneOnly.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lawyer Office Location", mapOnly.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Phone Number:", mapOnly.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("رقم هاتف المحامي:", mapOnly.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Office Location", withoutContact.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("موقع مكتب المحامي", withoutContact.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Phone Number:", withoutContact.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsultationCreatedConfirmation_RendersPhoneAndLocationMessageButNeverMap()
    {
        const string publicPhoneNumber = "01012345678";
        const string mapUrl =
            "https://www.google.com/maps/search/?api=1&query=30.044420,31.235712";
        var model = Consultation() with
        {
            LawyerPublicPhoneNumber = publicPhoneNumber,
            LawyerOfficeMapUrl = mapUrl
        };

        var created = _factory.Create(
            EmailNotificationType.ConsultationRequestCreatedConfirmation,
            model);
        var createdForLawyer = _factory.Create(
            EmailNotificationType.ConsultationRequestCreatedForLawyer,
            model);

        Assert.Contains("رقم هاتف المحامي:", created.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Lawyer Phone Number:", created.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(publicPhoneNumber, created.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Phone Number used for tracking:", created.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("01055555555", created.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(TrackingUrl, created.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Track Consultation Request", created.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("?reference", created.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "في حالة موافقة المحامي على طلب الاستشارة، سيتم إرسال موقع مكتب المحامي وفق القواعد الحالية للنظام.",
            created.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "If the lawyer approves your consultation request, the lawyer&#39;s office location will be sent according to the platform&#39;s current rules.",
            created.HtmlBody,
            StringComparison.Ordinal);
        Assert.DoesNotContain("google.com/maps", created.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Lawyer Office Location", created.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("عرض موقع المكتب على Google Maps", created.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain(publicPhoneNumber, createdForLawyer.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Phone Number:", createdForLawyer.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsultationCreatedConfirmation_WithoutPhoneOmitsPhoneAndKeepsLocationMessage()
    {
        var model = Consultation() with { LawyerPublicPhoneNumber = "   " };

        var result = _factory.Create(
            EmailNotificationType.ConsultationRequestCreatedConfirmation,
            model);

        Assert.DoesNotContain("رقم هاتف المحامي:", result.HtmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Lawyer Phone Number:", result.HtmlBody, StringComparison.Ordinal);
        Assert.Contains(
            "في حالة موافقة المحامي على طلب الاستشارة، سيتم إرسال موقع مكتب المحامي وفق القواعد الحالية للنظام.",
            result.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "If the lawyer approves your consultation request, the lawyer&#39;s office location will be sent according to the platform&#39;s current rules.",
            result.HtmlBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConsultationContactPhone_IsHtmlEncoded()
    {
        const string unsafePhone = "01012345678<script>alert('x')</script>&";
        var model = Consultation() with { LawyerPublicPhoneNumber = unsafePhone };

        var created = _factory.Create(
            EmailNotificationType.ConsultationRequestCreatedConfirmation,
            model);
        var approved = _factory.Create(EmailNotificationType.ConsultationApproved, model);

        Assert.DoesNotContain("<script>", created.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script>", approved.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "01012345678&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;&amp;",
            created.HtmlBody,
            StringComparison.Ordinal);
        Assert.Contains(
            "01012345678&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;&amp;",
            approved.HtmlBody,
            StringComparison.Ordinal);
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
        Assert.Throws<ArgumentException>(() =>
            _factory.Create(EmailNotificationType.ClientRegistrationWelcome, Client()));
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
            Reason: reason,
            RequesterPhoneNumber: "01055555555",
            ConsultationTrackingUrl: TrackingUrl);

    private static ClientEmailNotificationModel Client()
        => new("العميل Client");

    private static RegistrationWelcomeEmailNotificationModel Registration()
        => new("المستخدم Registrant", "registrant@example.test", "registrant.user");

    private static ContactInquiryEmailNotificationModel ContactInquiry()
        => new(
            "أحمد Ahmed",
            "01012345678",
            "ahmed@example.test",
            "Technical Support",
            "Details التفاصيل",
            new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc));

    private static int CountOccurrences(string value, string searchValue)
    {
        var count = 0;
        var searchIndex = 0;

        while ((searchIndex = value.IndexOf(searchValue, searchIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            searchIndex += searchValue.Length;
        }

        return count;
    }

    private sealed class FixedClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FixedEmailBrandingProvider(string footerImageUrl)
        : IEmailBrandingProvider
    {
        public string FooterImageUrl { get; } = footerImageUrl;
    }
}
