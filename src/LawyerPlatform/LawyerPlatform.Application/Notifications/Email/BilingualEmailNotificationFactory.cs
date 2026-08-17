using System.Net;
using System.Text;
using BuildingBlock.Application.Time;

namespace LawyerPlatform.Application.Notifications.Email;

internal sealed class BilingualEmailNotificationFactory(IDateTimeProvider clock)
    : IEmailNotificationFactory
{
    public EmailNotificationContent Create(
        EmailNotificationType notificationType,
        EmailNotificationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return notificationType switch
        {
            EmailNotificationType.LawyerSubmittedForApproval => LawyerSubmitted(RequireLawyer(model)),
            EmailNotificationType.LawyerApproved => LawyerApproved(RequireLawyer(model)),
            EmailNotificationType.LawyerChangesRequested => LawyerChangesRequested(RequireLawyer(model)),
            EmailNotificationType.LawyerRejected => LawyerRejected(RequireLawyer(model)),
            EmailNotificationType.LawyerSuspended => LawyerSuspended(RequireLawyer(model)),
            EmailNotificationType.LawyerReactivated => LawyerReactivated(RequireLawyer(model)),
            EmailNotificationType.ConsultationRequestCreatedForLawyer => ConsultationCreatedForLawyer(RequireConsultation(model)),
            EmailNotificationType.ConsultationRequestCreatedConfirmation => ConsultationCreatedConfirmation(RequireConsultation(model)),
            EmailNotificationType.ConsultationUnderReview => ConsultationUnderReview(RequireConsultation(model)),
            EmailNotificationType.ConsultationApproved => ConsultationApproved(RequireConsultation(model)),
            EmailNotificationType.ConsultationRejected => ConsultationRejected(RequireConsultation(model)),
            EmailNotificationType.ConsultationCompleted => ConsultationCompleted(RequireConsultation(model)),
            EmailNotificationType.ClientSuspended => ClientSuspended(RequireClient(model)),
            EmailNotificationType.ClientReactivated => ClientReactivated(RequireClient(model)),
            _ => throw new ArgumentOutOfRangeException(nameof(notificationType), notificationType, "Unknown email notification type.")
        };
    }

    private EmailNotificationContent LawyerSubmitted(LawyerEmailNotificationModel model)
        => Build(
            "Lawyer Platform | طلب محامٍ جديد للمراجعة | New Lawyer Application",
            [
                Lines("مرحبًا،"),
                Lines($"قام المحامي {model.LawyerName} بإرسال ملفه للمراجعة والاعتماد."),
                Lines("حالة الملف الحالية:", "قيد المراجعة"),
                Lines("يرجى الدخول إلى لوحة الإدارة لمراجعة بيانات المحامي والمستندات واتخاذ الإجراء المناسب."),
                Lines("رقم المحامي:", model.LawyerId.ToString()),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines("Hello,"),
                Lines($"Lawyer {model.LawyerName} has submitted their profile for review and approval."),
                Lines("Current status:", "Pending Approval"),
                Lines("Please sign in to the administration dashboard to review the lawyer's information and documents and take the appropriate action."),
                Lines("Lawyer ID:", model.LawyerId.ToString()),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent LawyerApproved(LawyerEmailNotificationModel model)
        => Build(
            "Lawyer Platform | تم اعتماد حسابك | Your Profile Has Been Approved",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("يسعدنا إبلاغك بأنه تم اعتماد ملفك بنجاح على Lawyer Platform."),
                Lines("حالة الملف:", "معتمد"),
                Lines("أصبح ملفك مؤهلًا للظهور للعملاء على المنصة وفقًا لقواعد ظهور المحامين المعتمدة."),
                Lines("يمكنك الآن تسجيل الدخول ومتابعة حسابك وطلبات الاستشارات الواردة إليك."),
                Lines("مع تمنياتنا لك بالتوفيق،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("We are pleased to inform you that your profile has been successfully approved on Lawyer Platform."),
                Lines("Profile status:", "Approved"),
                Lines("Your profile is now eligible to appear to clients on the platform, subject to the platform's lawyer visibility rules."),
                Lines("You can now sign in and manage your account and incoming consultation requests."),
                Lines("Best regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent LawyerChangesRequested(LawyerEmailNotificationModel model)
        => Build(
            "Lawyer Platform | مطلوب تعديلات على ملفك | Changes Required",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("تمت مراجعة ملفك بواسطة إدارة Lawyer Platform، وهناك بعض التعديلات المطلوبة قبل استكمال عملية الاعتماد."),
                Lines("حالة الملف:", "مطلوب تعديلات"),
                Lines("ملاحظات المراجعة:", RequireReason(model)),
                Lines("يرجى تسجيل الدخول إلى حسابك، مراجعة الملاحظات، وتحديث البيانات المطلوبة."),
                Lines("بعد الانتهاء يمكنك إرسال الملف للمراجعة مرة أخرى."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("Your profile has been reviewed by the Lawyer Platform administration, and some changes are required before the approval process can continue."),
                Lines("Profile status:", "Changes Requested"),
                Lines("Review notes:", RequireReason(model)),
                Lines("Please sign in to your account, review the notes, and update the required information."),
                Lines("Once completed, you can submit your profile for review again."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent LawyerRejected(LawyerEmailNotificationModel model)
        => Build(
            "Lawyer Platform | تحديث حالة طلب الاعتماد | Lawyer Application Update",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("نود إبلاغك بأنه بعد مراجعة ملفك، لم تتم الموافقة على طلب الاعتماد."),
                Lines("حالة الملف:", "مرفوض"),
                Lines("سبب الرفض:", RequireReason(model)),
                Lines("إذا كنت بحاجة إلى معلومات إضافية، يرجى التواصل مع إدارة Lawyer Platform."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("We would like to inform you that after reviewing your profile, your approval application has not been approved."),
                Lines("Profile status:", "Rejected"),
                Lines("Reason:", RequireReason(model)),
                Lines("If you require additional information, please contact the Lawyer Platform administration."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent LawyerSuspended(LawyerEmailNotificationModel model)
        => Build(
            "Lawyer Platform | تم تعليق حساب المحامي | Lawyer Profile Suspended",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("نود إبلاغك بأنه تم تعليق ملفك على Lawyer Platform."),
                Lines("حالة الملف:", "معلق"),
                Lines("سبب التعليق:", RequireReason(model)),
                Lines("أثناء فترة التعليق لن يكون ملفك متاحًا للعملاء على المنصة."),
                Lines("للاستفسار عن القرار أو الخطوات المطلوبة، يرجى التواصل مع إدارة Lawyer Platform."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("We would like to inform you that your lawyer profile on Lawyer Platform has been suspended."),
                Lines("Profile status:", "Suspended"),
                Lines("Reason:", RequireReason(model)),
                Lines("While your profile is suspended, it will not be available to clients on the platform."),
                Lines("For further information about this decision or any required actions, please contact the Lawyer Platform administration."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent LawyerReactivated(LawyerEmailNotificationModel model)
        => Build(
            "Lawyer Platform | تمت إعادة تفعيل ملفك | Your Lawyer Profile Has Been Reactivated",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("تمت إعادة تفعيل ملفك بنجاح على Lawyer Platform."),
                Lines("حالة الملف:", "معتمد"),
                Lines("أصبح ملفك مؤهلًا مرة أخرى للظهور للعملاء واستقبال طلبات الاستشارات وفقًا لقواعد المنصة."),
                Lines("مرحبًا بعودتك."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("Your lawyer profile has been successfully reactivated on Lawyer Platform."),
                Lines("Profile status:", "Approved"),
                Lines("Your profile is once again eligible to appear to clients and receive consultation requests according to the platform rules."),
                Lines("Welcome back."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent ConsultationCreatedForLawyer(ConsultationEmailNotificationModel model)
    {
        var arabic = new List<string>
        {
            Lines($"مرحبًا {model.LawyerName}،"),
            Lines("لديك طلب استشارة قانونية جديد على Lawyer Platform."),
            Lines("رقم الطلب:", model.ReferenceNumber),
            Lines("التخصص:", model.SpecializationNameAr),
            Lines("مقدم الطلب:", model.RequesterName)
        };
        var english = new List<string>
        {
            Lines($"Hello {model.LawyerName},"),
            Lines("You have received a new legal consultation request on Lawyer Platform."),
            Lines("Request Reference:", model.ReferenceNumber),
            Lines("Specialization:", model.SpecializationNameEn),
            Lines("Requested by:", model.RequesterName)
        };
        AddPreferredAppointment(model, arabic, english, "التاريخ المفضل للاستشارة:", "Preferred consultation date:");
        arabic.Add(Lines("يرجى تسجيل الدخول إلى حسابك لمراجعة تفاصيل الطلب واتخاذ الإجراء المناسب."));
        arabic.Add(Lines("للحفاظ على خصوصية مقدم الطلب، لا يتم إرسال وصف الاستشارة أو البيانات الحساسة عبر البريد الإلكتروني."));
        arabic.Add(Lines("مع تحيات،", "Lawyer Platform"));
        english.Add(Lines("Please sign in to your account to review the request details and take the appropriate action."));
        english.Add(Lines("To protect the requester's privacy, consultation descriptions and sensitive information are not included in email notifications."));
        english.Add(Lines("Regards,", "Lawyer Platform"));
        return Build("Lawyer Platform | طلب استشارة جديد | New Consultation Request", arabic, english);
    }

    private EmailNotificationContent ConsultationCreatedConfirmation(ConsultationEmailNotificationModel model)
        => Build(
            "Lawyer Platform | تم استلام طلب الاستشارة | Consultation Request Received",
            [
                Lines($"مرحبًا {model.RequesterName}،"),
                Lines("تم استلام طلب الاستشارة الخاص بك بنجاح على Lawyer Platform."),
                Lines("رقم متابعة الطلب:", model.ReferenceNumber),
                Lines("المحامي:", model.LawyerName),
                Lines("التخصص:", model.SpecializationNameAr),
                Lines("حالة الطلب:", "جديد"),
                Lines("احتفظ برقم متابعة الطلب، فقد تحتاج إليه لمتابعة حالة طلبك."),
                Lines("سنقوم بإبلاغك عند حدوث تحديثات مهمة على حالة الطلب."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.RequesterName},"),
                Lines("Your consultation request has been successfully received by Lawyer Platform."),
                Lines("Request Reference:", model.ReferenceNumber),
                Lines("Lawyer:", model.LawyerName),
                Lines("Specialization:", model.SpecializationNameEn),
                Lines("Current status:", "New"),
                Lines("Please keep your request reference as you may need it to track your request."),
                Lines("We will notify you when important updates are made to your request status."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent ConsultationUnderReview(ConsultationEmailNotificationModel model)
        => Build(
            "Lawyer Platform | طلب الاستشارة قيد المراجعة | Consultation Under Review",
            [
                Lines($"مرحبًا {model.RequesterName}،"),
                Lines("هناك تحديث جديد على طلب الاستشارة الخاص بك."),
                Lines("رقم الطلب:", model.ReferenceNumber),
                Lines("المحامي:", model.LawyerName),
                Lines("الحالة الجديدة:", "قيد المراجعة"),
                Lines("بدأ المحامي في مراجعة طلب الاستشارة الخاص بك."),
                Lines("سنقوم بإبلاغك عند حدوث تحديث جديد على الطلب."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.RequesterName},"),
                Lines("There is a new update regarding your consultation request."),
                Lines("Request Reference:", model.ReferenceNumber),
                Lines("Lawyer:", model.LawyerName),
                Lines("New status:", "Under Review"),
                Lines("The lawyer has started reviewing your consultation request."),
                Lines("We will notify you when there is another important update."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent ConsultationApproved(ConsultationEmailNotificationModel model)
    {
        var arabic = new List<string>
        {
            Lines($"مرحبًا {model.RequesterName}،"),
            Lines("يسعدنا إبلاغك بأنه تمت الموافقة على طلب الاستشارة الخاص بك."),
            Lines("رقم الطلب:", model.ReferenceNumber),
            Lines("المحامي:", model.LawyerName),
            Lines("التخصص:", model.SpecializationNameAr),
            Lines("الحالة:", "تمت الموافقة")
        };
        var english = new List<string>
        {
            Lines($"Hello {model.RequesterName},"),
            Lines("We are pleased to inform you that your consultation request has been approved."),
            Lines("Request Reference:", model.ReferenceNumber),
            Lines("Lawyer:", model.LawyerName),
            Lines("Specialization:", model.SpecializationNameEn),
            Lines("Status:", "Approved")
        };
        AddPreferredAppointment(model, arabic, english, "التاريخ المفضل المسجل:", "Recorded preferred date:");
        arabic.Add(Lines("يمكنك متابعة حالة الطلب من خلال Lawyer Platform."));
        arabic.Add(Lines("مع تحيات،", "Lawyer Platform"));
        english.Add(Lines("You can continue tracking your request through Lawyer Platform."));
        english.Add(Lines("Regards,", "Lawyer Platform"));
        return Build("Lawyer Platform | تمت الموافقة على طلب الاستشارة | Consultation Request Approved", arabic, english);
    }

    private EmailNotificationContent ConsultationRejected(ConsultationEmailNotificationModel model)
        => Build(
            "Lawyer Platform | تحديث طلب الاستشارة | Consultation Request Update",
            [
                Lines($"مرحبًا {model.RequesterName}،"),
                Lines("نود إبلاغك بوجود تحديث على طلب الاستشارة الخاص بك."),
                Lines("رقم الطلب:", model.ReferenceNumber),
                Lines("المحامي:", model.LawyerName),
                Lines("الحالة:", "مرفوض"),
                Lines("سبب الرفض:", RequireReason(model)),
                Lines("يمكنك الرجوع إلى Lawyer Platform للبحث عن محامٍ آخر مناسب إذا رغبت."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.RequesterName},"),
                Lines("We would like to inform you of an update to your consultation request."),
                Lines("Request Reference:", model.ReferenceNumber),
                Lines("Lawyer:", model.LawyerName),
                Lines("Status:", "Rejected"),
                Lines("Reason:", RequireReason(model)),
                Lines("You may return to Lawyer Platform to find another suitable lawyer if needed."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent ConsultationCompleted(ConsultationEmailNotificationModel model)
        => Build(
            "Lawyer Platform | تم إكمال طلب الاستشارة | Consultation Completed",
            [
                Lines($"مرحبًا {model.RequesterName}،"),
                Lines("تم تحديث طلب الاستشارة الخاص بك إلى مكتمل."),
                Lines("رقم الطلب:", model.ReferenceNumber),
                Lines("المحامي:", model.LawyerName),
                Lines("الحالة:", "مكتمل"),
                Lines("نشكرك على استخدام Lawyer Platform."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.RequesterName},"),
                Lines("Your consultation request has been marked as completed."),
                Lines("Request Reference:", model.ReferenceNumber),
                Lines("Lawyer:", model.LawyerName),
                Lines("Status:", "Completed"),
                Lines("Thank you for using Lawyer Platform."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent ClientSuspended(ClientEmailNotificationModel model)
        => Build(
            "Lawyer Platform | تم تعليق حسابك | Your Account Has Been Suspended",
            [
                Lines($"مرحبًا {model.ClientName}،"),
                Lines("نود إبلاغك بأنه تم تعليق حسابك على Lawyer Platform."),
                Lines("حالة الحساب:", "معلق"),
                Lines("لن تتمكن من استخدام الوظائف التي تتطلب حسابًا نشطًا أثناء فترة التعليق."),
                Lines("للحصول على معلومات إضافية، يرجى التواصل مع إدارة Lawyer Platform."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.ClientName},"),
                Lines("We would like to inform you that your Lawyer Platform account has been suspended."),
                Lines("Account status:", "Suspended"),
                Lines("While your account is suspended, features requiring an active account will not be available."),
                Lines("For additional information, please contact the Lawyer Platform administration."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent ClientReactivated(ClientEmailNotificationModel model)
        => Build(
            "Lawyer Platform | تمت إعادة تفعيل حسابك | Your Account Has Been Reactivated",
            [
                Lines($"مرحبًا {model.ClientName}،"),
                Lines("تمت إعادة تفعيل حسابك بنجاح على Lawyer Platform."),
                Lines("حالة الحساب:", "نشط"),
                Lines("يمكنك الآن تسجيل الدخول واستخدام خدمات حسابك مرة أخرى."),
                Lines("مرحبًا بعودتك."),
                Lines("مع تحيات،", "Lawyer Platform")
            ],
            [
                Lines($"Hello {model.ClientName},"),
                Lines("Your Lawyer Platform account has been successfully reactivated."),
                Lines("Account status:", "Active"),
                Lines("You can now sign in and use your account services again."),
                Lines("Welcome back."),
                Lines("Regards,", "Lawyer Platform")
            ]);

    private EmailNotificationContent Build(
        string subject,
        IEnumerable<string> arabicParagraphs,
        IEnumerable<string> englishParagraphs)
    {
        var body = new StringBuilder(2048);
        body.Append("<!doctype html><html><head><meta charset=\"utf-8\"></head>")
            .Append("<body style=\"font-family:Arial,sans-serif;color:#222;line-height:1.6\">")
            .Append("<div style=\"max-width:680px;margin:0 auto\">")
            .Append("<h2 style=\"margin-bottom:24px\">Lawyer Platform</h2>")
            .Append("<section dir=\"rtl\" lang=\"ar\" style=\"text-align:right\">")
            .AppendJoin(string.Empty, arabicParagraphs)
            .Append("</section><hr style=\"border:0;border-top:1px solid #bbb;margin:28px 0\">")
            .Append("<section dir=\"ltr\" lang=\"en\" style=\"text-align:left\">")
            .AppendJoin(string.Empty, englishParagraphs)
            .Append("</section><footer style=\"margin-top:28px;color:#666\">")
            .Append("Lawyer Platform<br>&copy; ")
            .Append(clock.UtcNow.Year)
            .Append("</footer></div></body></html>");
        return new EmailNotificationContent(subject, body.ToString());
    }

    private static string Lines(params string[] values)
        => $"<p>{string.Join("<br>", values.Select(WebUtility.HtmlEncode))}</p>";

    private static void AddPreferredAppointment(
        ConsultationEmailNotificationModel model,
        List<string> arabic,
        List<string> english,
        string arabicLabel,
        string englishLabel)
    {
        if (model.PreferredAppointmentOnUtc is not { } preferred)
        {
            return;
        }

        var utc = preferred.Kind == DateTimeKind.Utc ? preferred : preferred.ToUniversalTime();
        var formatted = utc.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        arabic.Add(Lines(arabicLabel, formatted));
        english.Add(Lines(englishLabel, formatted));
    }

    private static LawyerEmailNotificationModel RequireLawyer(EmailNotificationModel model)
        => model as LawyerEmailNotificationModel
           ?? throw new ArgumentException("A lawyer notification model is required.", nameof(model));

    private static ConsultationEmailNotificationModel RequireConsultation(EmailNotificationModel model)
        => model as ConsultationEmailNotificationModel
           ?? throw new ArgumentException("A consultation notification model is required.", nameof(model));

    private static ClientEmailNotificationModel RequireClient(EmailNotificationModel model)
        => model as ClientEmailNotificationModel
           ?? throw new ArgumentException("A client notification model is required.", nameof(model));

    private static string RequireReason(LawyerEmailNotificationModel model)
        => !string.IsNullOrWhiteSpace(model.Reason)
            ? model.Reason
            : throw new ArgumentException("A reason is required for this notification.", nameof(model));

    private static string RequireReason(ConsultationEmailNotificationModel model)
        => !string.IsNullOrWhiteSpace(model.Reason)
            ? model.Reason
            : throw new ArgumentException("A reason is required for this notification.", nameof(model));
}
