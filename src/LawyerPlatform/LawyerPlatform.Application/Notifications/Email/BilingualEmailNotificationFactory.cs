using System.Net;
using System.Text;
using BuildingBlock.Application.Time;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Notifications.Email;

internal sealed class BilingualEmailNotificationFactory(
    IDateTimeProvider clock,
    IEmailBrandingProvider emailBranding)
    : IEmailNotificationFactory
{
    private const string GoogleMapsUrlPrefix = "https://www.google.com/maps/search/?api=1&query=";

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
            EmailNotificationType.LawyerRegistrationWelcome => LawyerRegistrationWelcome(RequireRegistration(model)),
            EmailNotificationType.ClientRegistrationWelcome => ClientRegistrationWelcome(RequireRegistration(model)),
            EmailNotificationType.ContactInquirySupportNotification => ContactInquirySupportNotification(RequireContactInquiry(model)),
            EmailNotificationType.ContactInquiryConfirmation => ContactInquiryConfirmation(RequireContactInquiry(model)),
            _ => throw new ArgumentOutOfRangeException(nameof(notificationType), notificationType, "Unknown email notification type.")
        };
    }

    private EmailNotificationContent ContactInquirySupportNotification(
        ContactInquiryEmailNotificationModel model)
    {
        var createdOnUtc = model.CreatedOnUtc
            .ToUniversalTime()
            .ToString("yyyy-MM-dd HH:mm:ss 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

        return Build(
            "Avokatoo | استفسار تواصل جديد | New Contact Inquiry",
            [
                Lines("مرحبًا فريق الدعم،"),
                Lines("تم استلام استفسار تواصل جديد."),
                Lines("الاسم الكامل:", model.FullName),
                Lines("رقم الهاتف:", model.PhoneNumber),
                Lines("البريد الإلكتروني:", model.Email),
                Lines("نوع الاستفسار:", model.InquiryType),
                MessageLines("الرسالة / التفاصيل:", model.Message),
                Lines("وقت الإنشاء:", createdOnUtc),
                Lines("Avokatoo")
            ],
            [
                Lines("A new contact inquiry has been received."),
                Lines("Full Name:", model.FullName),
                Lines("Phone Number:", model.PhoneNumber),
                Lines("Email:", model.Email),
                Lines("Inquiry Type:", model.InquiryType),
                MessageLines("Message / Details:", model.Message),
                Lines("Created On:", createdOnUtc),
                Lines("Avokatoo")
            ]);
    }

    private EmailNotificationContent ContactInquiryConfirmation(
        ContactInquiryEmailNotificationModel model)
        => Build(
            "Avokatoo | تم استلام استفسارك | Inquiry Received",
            [
                Lines($"مرحبًا {model.FullName}،"),
                Lines("تم استلام استفسارك بنجاح."),
                Lines("سيقوم فريق Avokatoo بمراجعة رسالتك والتواصل معك عند الحاجة."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.FullName},"),
                Lines("We have successfully received your inquiry."),
                Lines("The Avokatoo team will review your message and contact you when needed."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent LawyerRegistrationWelcome(RegistrationWelcomeEmailNotificationModel model)
        => Build(
            "Avokatoo | مرحبًا بك كمحامٍ | Welcome to Avokatoo",
            [
                Lines($"مرحبًا {model.FullName}،"),
                Lines("أهلًا بك في Avokatoo."),
                Lines("تم إنشاء حساب المحامي الخاص بك بنجاح."),
                Lines("بيانات حسابك:"),
                Lines("البريد الإلكتروني:", model.Email),
                Lines("اسم المستخدم:", model.UserName),
                Lines("حالة ملف المحامي الحالية:", "مسودة"),
                Lines("حتى يظهر ملفك للعملاء على منصة Avokatoo، يجب استكمال ملفك المهني وإرساله للمراجعة والاعتماد."),
                Lines(
                    "الخطوات المطلوبة:",
                    "1. تسجيل الدخول إلى حسابك.",
                    "2. استكمال بيانات الملف المهني.",
                    "3. إضافة بيانات وموقع المكتب.",
                    "4. اختيار التخصصات القانونية.",
                    "5. رفع المستندات المهنية المطلوبة.",
                    "6. إرسال الملف للمراجعة من خلال Submit For Approval."),
                Lines("بعد إرسال الملف، سيقوم فريق الإدارة بمراجعته."),
                Lines("لن يظهر ملفك في البحث العام ولن يتم اعتباره ملف محامٍ معتمد حتى تتم الموافقة عليه من الإدارة وفقًا لقواعد المنصة."),
                Lines("نتطلع إلى وجودك معنا على Avokatoo."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.FullName},"),
                Lines("Welcome to Avokatoo."),
                Lines("Your Lawyer account has been created successfully."),
                Lines("Your account details:"),
                Lines("Email:", model.Email),
                Lines("User Name:", model.UserName),
                Lines("Current Lawyer profile status:", "Draft"),
                Lines("To make your Lawyer profile available to clients on Avokatoo, you must complete your professional profile and submit it for review and approval."),
                Lines(
                    "Next steps:",
                    "1. Sign in to your account.",
                    "2. Complete your professional profile.",
                    "3. Add your office information and location.",
                    "4. Select your legal specializations.",
                    "5. Upload the required professional documents.",
                    "6. Submit your profile using Submit For Approval."),
                Lines("After submission, the administration team will review your profile."),
                Lines("Your profile will not appear in public Lawyer search and will not be considered approved until the administration approves it according to the platform rules."),
                Lines("We look forward to having you on Avokatoo."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent ClientRegistrationWelcome(RegistrationWelcomeEmailNotificationModel model)
        => Build(
            "Avokatoo | مرحبًا بك | Welcome to Avokatoo",
            [
                Lines($"مرحبًا {model.FullName}،"),
                Lines("أهلًا بك في Avokatoo."),
                Lines("تم إنشاء حسابك بنجاح."),
                Lines("بيانات حسابك:"),
                Lines("البريد الإلكتروني:", model.Email),
                Lines("اسم المستخدم:", model.UserName),
                Lines("يمكنك الآن تسجيل الدخول إلى حسابك والاستفادة من خدمات المنصة، بما في ذلك البحث عن المحامين، الاطلاع على ملفات المحامين المعتمدين، إرسال طلبات الاستشارة، ومتابعة طلباتك الحالية والسابقة."),
                Lines("يسعدنا انضمامك إلى Avokatoo."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.FullName},"),
                Lines("Welcome to Avokatoo."),
                Lines("Your account has been created successfully."),
                Lines("Your account details:"),
                Lines("Email:", model.Email),
                Lines("User Name:", model.UserName),
                Lines("You can now sign in and use Avokatoo services, including searching for Lawyers, viewing approved Lawyer profiles, submitting consultation requests, and following your current and previous requests."),
                Lines("We are happy to have you with us."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent LawyerSubmitted(LawyerEmailNotificationModel model)
        => Build(
            "Avokatoo | طلب محامٍ جديد للمراجعة | New Lawyer Application",
            [
                Lines("مرحبًا،"),
                Lines($"قام المحامي {model.LawyerName} بإرسال ملفه للمراجعة والاعتماد."),
                Lines("حالة الملف الحالية:", "قيد المراجعة"),
                Lines("يرجى الدخول إلى لوحة الإدارة لمراجعة بيانات المحامي والمستندات واتخاذ الإجراء المناسب."),
                Lines("رقم المحامي:", model.LawyerId.ToString()),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines("Hello,"),
                Lines($"Lawyer {model.LawyerName} has submitted their profile for review and approval."),
                Lines("Current status:", "Pending Approval"),
                Lines("Please sign in to the administration dashboard to review the lawyer's information and documents and take the appropriate action."),
                Lines("Lawyer ID:", model.LawyerId.ToString()),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent LawyerApproved(LawyerEmailNotificationModel model)
        => Build(
            "Avokatoo | تم اعتماد حسابك | Your Profile Has Been Approved",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("يسعدنا إبلاغك بأنه تم اعتماد ملفك بنجاح على Avokatoo."),
                Lines("حالة الملف:", "معتمد"),
                Lines("أصبح ملفك مؤهلًا للظهور للعملاء على المنصة وفقًا لقواعد ظهور المحامين المعتمدة."),
                Lines("يمكنك الآن تسجيل الدخول ومتابعة حسابك وطلبات الاستشارات الواردة إليك."),
                Lines("مع تمنياتنا لك بالتوفيق،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("We are pleased to inform you that your profile has been successfully approved on Avokatoo."),
                Lines("Profile status:", "Approved"),
                Lines("Your profile is now eligible to appear to clients on the platform, subject to the platform's lawyer visibility rules."),
                Lines("You can now sign in and manage your account and incoming consultation requests."),
                Lines("Best regards,", "Avokatoo")
            ]);

    private EmailNotificationContent LawyerChangesRequested(LawyerEmailNotificationModel model)
        => Build(
            "Avokatoo | مطلوب تعديلات على ملفك | Changes Required",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("تمت مراجعة ملفك بواسطة إدارة Avokatoo، وهناك بعض التعديلات المطلوبة قبل استكمال عملية الاعتماد."),
                Lines("حالة الملف:", "مطلوب تعديلات"),
                Lines("ملاحظات المراجعة:", RequireReason(model)),
                Lines("يرجى تسجيل الدخول إلى حسابك، مراجعة الملاحظات، وتحديث البيانات المطلوبة."),
                Lines("بعد الانتهاء يمكنك إرسال الملف للمراجعة مرة أخرى."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("Your profile has been reviewed by the Avokatoo administration, and some changes are required before the approval process can continue."),
                Lines("Profile status:", "Changes Requested"),
                Lines("Review notes:", RequireReason(model)),
                Lines("Please sign in to your account, review the notes, and update the required information."),
                Lines("Once completed, you can submit your profile for review again."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent LawyerRejected(LawyerEmailNotificationModel model)
        => Build(
            "Avokatoo | تحديث حالة طلب الاعتماد | Lawyer Application Update",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("نود إبلاغك بأنه بعد مراجعة ملفك، لم تتم الموافقة على طلب الاعتماد."),
                Lines("حالة الملف:", "مرفوض"),
                Lines("سبب الرفض:", RequireReason(model)),
                Lines("إذا كنت بحاجة إلى معلومات إضافية، يرجى التواصل مع إدارة Avokatoo."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("We would like to inform you that after reviewing your profile, your approval application has not been approved."),
                Lines("Profile status:", "Rejected"),
                Lines("Reason:", RequireReason(model)),
                Lines("If you require additional information, please contact the Avokatoo administration."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent LawyerSuspended(LawyerEmailNotificationModel model)
        => Build(
            "Avokatoo | تم تعليق حساب المحامي | Lawyer Profile Suspended",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("نود إبلاغك بأنه تم تعليق ملفك على Avokatoo."),
                Lines("حالة الملف:", "معلق"),
                Lines("سبب التعليق:", RequireReason(model)),
                Lines("أثناء فترة التعليق لن يكون ملفك متاحًا للعملاء على المنصة."),
                Lines("للاستفسار عن القرار أو الخطوات المطلوبة، يرجى التواصل مع إدارة Avokatoo."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("We would like to inform you that your lawyer profile on Avokatoo has been suspended."),
                Lines("Profile status:", "Suspended"),
                Lines("Reason:", RequireReason(model)),
                Lines("While your profile is suspended, it will not be available to clients on the platform."),
                Lines("For further information about this decision or any required actions, please contact the Avokatoo administration."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent LawyerReactivated(LawyerEmailNotificationModel model)
        => Build(
            "Avokatoo | تمت إعادة تفعيل ملفك | Your Lawyer Profile Has Been Reactivated",
            [
                Lines($"مرحبًا {model.LawyerName}،"),
                Lines("تمت إعادة تفعيل ملفك بنجاح على Avokatoo."),
                Lines("حالة الملف:", "معتمد"),
                Lines("أصبح ملفك مؤهلًا مرة أخرى للظهور للعملاء واستقبال طلبات الاستشارات وفقًا لقواعد المنصة."),
                Lines("مرحبًا بعودتك."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.LawyerName},"),
                Lines("Your lawyer profile has been successfully reactivated on Avokatoo."),
                Lines("Profile status:", "Approved"),
                Lines("Your profile is once again eligible to appear to clients and receive consultation requests according to the platform rules."),
                Lines("Welcome back."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent ConsultationCreatedForLawyer(ConsultationEmailNotificationModel model)
    {
        var arabic = new List<string>
        {
            Lines($"مرحبًا {model.LawyerName}،"),
            Lines("لديك طلب استشارة قانونية جديد على Avokatoo."),
            Lines("رقم الطلب:", model.ReferenceNumber),
            Lines("التخصص:", model.SpecializationNameAr),
            Lines("مقدم الطلب:", model.RequesterName)
        };
        var english = new List<string>
        {
            Lines($"Hello {model.LawyerName},"),
            Lines("You have received a new legal consultation request on Avokatoo."),
            Lines("Request Reference:", model.ReferenceNumber),
            Lines("Specialization:", model.SpecializationNameEn),
            Lines("Requested by:", model.RequesterName)
        };
        AddPreferredAppointment(model, arabic, english, "التاريخ المفضل للاستشارة:", "Preferred consultation date:");
        arabic.Add(Lines("يرجى تسجيل الدخول إلى حسابك لمراجعة تفاصيل الطلب واتخاذ الإجراء المناسب."));
        arabic.Add(Lines("للحفاظ على خصوصية مقدم الطلب، لا يتم إرسال وصف الاستشارة أو البيانات الحساسة عبر البريد الإلكتروني."));
        arabic.Add(Lines("مع تحيات،", "Avokatoo"));
        english.Add(Lines("Please sign in to your account to review the request details and take the appropriate action."));
        english.Add(Lines("To protect the requester's privacy, consultation descriptions and sensitive information are not included in email notifications."));
        english.Add(Lines("Regards,", "Avokatoo"));
        return BuildConsultation(
            "Avokatoo | طلب استشارة جديد | New Consultation Request",
            model,
            arabic,
            english);
    }

    private EmailNotificationContent ConsultationCreatedConfirmation(ConsultationEmailNotificationModel model)
    {
        var arabic = new List<string>
        {
            Lines($"مرحبًا {model.RequesterName}،"),
            Lines("تم استلام طلب الاستشارة الخاص بك بنجاح على Avokatoo."),
            Lines("رقم متابعة الطلب:", model.ReferenceNumber),
            Lines("المحامي:", model.LawyerName),
            Lines("التخصص:", model.SpecializationNameAr),
            Lines("حالة الطلب:", "جديد")
        };
        var english = new List<string>
        {
            Lines($"Hello {model.RequesterName},"),
            Lines("Your consultation request has been successfully received by Avokatoo."),
            Lines("Request Reference:", model.ReferenceNumber),
            Lines("Lawyer:", model.LawyerName),
            Lines("Specialization:", model.SpecializationNameEn),
            Lines("Current status:", "New")
        };
        AddLawyerPublicPhone(model, arabic, english);
        arabic.Add(Lines("في حالة موافقة المحامي على طلب الاستشارة، سيتم إرسال موقع مكتب المحامي إليك."));
        arabic.Add(Lines("احتفظ برقم متابعة الطلب، فقد تحتاج إليه لمتابعة حالة طلبك."));
        arabic.Add(Lines("سنقوم بإبلاغك عند حدوث تحديثات مهمة على حالة الطلب."));
        arabic.Add(Lines("مع تحيات،", "Avokatoo"));
        english.Add(Lines("If the lawyer approves your consultation request, the lawyer's office location will be sent to you."));
        english.Add(Lines("Please keep your request reference as you may need it to track your request."));
        english.Add(Lines("We will notify you when important updates are made to your request status."));
        english.Add(Lines("Regards,", "Avokatoo"));
        return BuildConsultation(
            "Avokatoo | تم استلام طلب الاستشارة | Consultation Request Received",
            model,
            arabic,
            english);
    }

    private EmailNotificationContent ConsultationUnderReview(ConsultationEmailNotificationModel model)
        => BuildConsultation(
            "Avokatoo | طلب الاستشارة قيد المراجعة | Consultation Under Review",
            model,
            [
                Lines($"مرحبًا {model.RequesterName}،"),
                Lines("هناك تحديث جديد على طلب الاستشارة الخاص بك."),
                Lines("رقم الطلب:", model.ReferenceNumber),
                Lines("المحامي:", model.LawyerName),
                Lines("الحالة الجديدة:", "قيد المراجعة"),
                Lines("بدأ المحامي في مراجعة طلب الاستشارة الخاص بك."),
                Lines("سنقوم بإبلاغك عند حدوث تحديث جديد على الطلب."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.RequesterName},"),
                Lines("There is a new update regarding your consultation request."),
                Lines("Request Reference:", model.ReferenceNumber),
                Lines("Lawyer:", model.LawyerName),
                Lines("New status:", "Under Review"),
                Lines("The lawyer has started reviewing your consultation request."),
                Lines("We will notify you when there is another important update."),
                Lines("Regards,", "Avokatoo")
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
        AddLawyerPublicPhone(model, arabic, english);
        AddLawyerOfficeMapLocation(model, arabic, english);
        arabic.Add(Lines("يمكنك متابعة حالة الطلب من خلال Avokatoo."));
        arabic.Add(Lines("مع تحيات،", "Avokatoo"));
        english.Add(Lines("You can continue tracking your request through Avokatoo."));
        english.Add(Lines("Regards,", "Avokatoo"));
        return BuildConsultation(
            "Avokatoo | تمت الموافقة على طلب الاستشارة | Consultation Request Approved",
            model,
            arabic,
            english);
    }

    private EmailNotificationContent ConsultationRejected(ConsultationEmailNotificationModel model)
        => BuildConsultation(
            "Avokatoo | تحديث طلب الاستشارة | Consultation Request Update",
            model,
            [
                Lines($"مرحبًا {model.RequesterName}،"),
                Lines("نود إبلاغك بوجود تحديث على طلب الاستشارة الخاص بك."),
                Lines("رقم الطلب:", model.ReferenceNumber),
                Lines("المحامي:", model.LawyerName),
                Lines("الحالة:", "مرفوض"),
                Lines("سبب الرفض:", RequireReason(model)),
                Lines("يمكنك الرجوع إلى Avokatoo للبحث عن محامٍ آخر مناسب إذا رغبت."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.RequesterName},"),
                Lines("We would like to inform you of an update to your consultation request."),
                Lines("Request Reference:", model.ReferenceNumber),
                Lines("Lawyer:", model.LawyerName),
                Lines("Status:", "Rejected"),
                Lines("Reason:", RequireReason(model)),
                Lines("You may return to Avokatoo to find another suitable lawyer if needed."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent ConsultationCompleted(ConsultationEmailNotificationModel model)
        => BuildConsultation(
            "Avokatoo | تم إكمال طلب الاستشارة | Consultation Completed",
            model,
            [
                Lines($"مرحبًا {model.RequesterName}،"),
                Lines("تم تحديث طلب الاستشارة الخاص بك إلى مكتمل."),
                Lines("رقم الطلب:", model.ReferenceNumber),
                Lines("المحامي:", model.LawyerName),
                Lines("الحالة:", "مكتمل"),
                Lines("نشكرك على استخدام Avokatoo."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.RequesterName},"),
                Lines("Your consultation request has been marked as completed."),
                Lines("Request Reference:", model.ReferenceNumber),
                Lines("Lawyer:", model.LawyerName),
                Lines("Status:", "Completed"),
                Lines("Thank you for using Avokatoo."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent ClientSuspended(ClientEmailNotificationModel model)
        => Build(
            "Avokatoo | تم تعليق حسابك | Your Account Has Been Suspended",
            [
                Lines($"مرحبًا {model.ClientName}،"),
                Lines("نود إبلاغك بأنه تم تعليق حسابك على Avokatoo."),
                Lines("حالة الحساب:", "معلق"),
                Lines("لن تتمكن من استخدام الوظائف التي تتطلب حسابًا نشطًا أثناء فترة التعليق."),
                Lines("للحصول على معلومات إضافية، يرجى التواصل مع إدارة Avokatoo."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.ClientName},"),
                Lines("We would like to inform you that your Avokatoo account has been suspended."),
                Lines("Account status:", "Suspended"),
                Lines("While your account is suspended, features requiring an active account will not be available."),
                Lines("For additional information, please contact the Avokatoo administration."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent ClientReactivated(ClientEmailNotificationModel model)
        => Build(
            "Avokatoo | تمت إعادة تفعيل حسابك | Your Account Has Been Reactivated",
            [
                Lines($"مرحبًا {model.ClientName}،"),
                Lines("تمت إعادة تفعيل حسابك بنجاح على Avokatoo."),
                Lines("حالة الحساب:", "نشط"),
                Lines("يمكنك الآن تسجيل الدخول واستخدام خدمات حسابك مرة أخرى."),
                Lines("مرحبًا بعودتك."),
                Lines("مع تحيات،", "Avokatoo")
            ],
            [
                Lines($"Hello {model.ClientName},"),
                Lines("Your Avokatoo account has been successfully reactivated."),
                Lines("Account status:", "Active"),
                Lines("You can now sign in and use your account services again."),
                Lines("Welcome back."),
                Lines("Regards,", "Avokatoo")
            ]);

    private EmailNotificationContent Build(
        string subject,
        IEnumerable<string> arabicParagraphs,
        IEnumerable<string> englishParagraphs)
    {
        var body = new StringBuilder(2048);
        body.Append("<!doctype html><html><head><meta charset=\"utf-8\"></head>")
            .Append("<body style=\"font-family:Arial,sans-serif;color:#222;font-size:20px;line-height:1.7\">")
            .Append("<div style=\"max-width:680px;margin:0 auto\">")
            .Append("<h2 style=\"font-size:28px;margin-bottom:24px\">Avokatoo</h2>")
            .Append("<section dir=\"rtl\" lang=\"ar\" style=\"text-align:right\">")
            .AppendJoin(string.Empty, arabicParagraphs)
            .Append("</section><hr style=\"border:0;border-top:1px solid #bbb;margin:28px 0\">")
            .Append("<section dir=\"ltr\" lang=\"en\" style=\"text-align:left\">")
            .AppendJoin(string.Empty, englishParagraphs)
            .Append("</section><footer style=\"margin-top:28px;color:#666;font-size:20px\">")
            .Append("Avokatoo<br>&copy; ")
            .Append(clock.UtcNow.Year)
            .Append("</footer>")
            .Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" style=\"margin-top:24px;\">")
            .Append("<tr><td align=\"center\">")
            .Append("<img src=\"")
            .Append(WebUtility.HtmlEncode(emailBranding.FooterImageUrl))
            .Append("\" alt=\"Avokatoo\" width=\"700\" style=\"display:block;width:100%;max-width:700px;height:auto;border:0;outline:none;text-decoration:none;\">")
            .Append("</td></tr></table></div></body></html>");
        return new EmailNotificationContent(subject, body.ToString());
    }

    private EmailNotificationContent BuildConsultation(
        string subject,
        ConsultationEmailNotificationModel model,
        IEnumerable<string> arabicParagraphs,
        IEnumerable<string> englishParagraphs)
    {
        var arabic = arabicParagraphs.ToList();
        var english = englishParagraphs.ToList();
        var insertAt = Math.Min(1, arabic.Count);
        arabic.Insert(insertAt, Lines(
            "نوع الاستشارة:",
            model.ConsultationType == ConsultationType.Online ? "عن بُعد" : "في المكتب"));
        english.Insert(Math.Min(1, english.Count), Lines(
            "Consultation type:",
            model.ConsultationType.ToString()));

        if (model.ConsultationType == ConsultationType.Online && model.ConsultationPrice.HasValue)
        {
            var price = model.ConsultationPrice.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            arabic.Insert(Math.Min(2, arabic.Count), Lines("سعر الاستشارة:", $"{price} EGP"));
            english.Insert(Math.Min(2, english.Count), Lines("Consultation price:", $"{price} EGP"));
        }

        return Build(subject, arabic, english);
    }

    private static string Lines(params string[] values)
        => $"<p>{string.Join("<br>", values.Select(WebUtility.HtmlEncode))}</p>";

    private static string MessageLines(string label, string message)
    {
        var lines = message
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        return Lines([label, .. lines]);
    }

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

    private static void AddLawyerOfficeMapLocation(
        ConsultationEmailNotificationModel model,
        List<string> arabic,
        List<string> english)
    {
        var safeUrl = GetSafeGoogleMapsUrl(model.LawyerOfficeMapUrl);
        if (safeUrl is null)
        {
            return;
        }

        arabic.Add(Lines(
            "موقع مكتب المحامي",
            "يمكنك الوصول إلى موقع مكتب المحامي من خلال Google Maps:"));
        arabic.Add(Link(safeUrl, "عرض موقع المكتب على Google Maps"));
        english.Add(Lines(
            "Lawyer Office Location",
            "You can view the lawyer's office location on Google Maps:"));
        english.Add(Link(safeUrl, "View Lawyer Office on Google Maps"));
    }

    private static void AddLawyerPublicPhone(
        ConsultationEmailNotificationModel model,
        List<string> arabic,
        List<string> english)
    {
        if (string.IsNullOrWhiteSpace(model.LawyerPublicPhoneNumber))
        {
            return;
        }

        arabic.Add(Lines("رقم هاتف المحامي:", model.LawyerPublicPhoneNumber));
        english.Add(Lines("Lawyer Phone Number:", model.LawyerPublicPhoneNumber));
    }

    private static string? GetSafeGoogleMapsUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(GoogleMapsUrlPrefix, StringComparison.Ordinal) ||
            !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "www.google.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    private static string Link(string url, string text)
        => $"<p><a href=\"{WebUtility.HtmlEncode(url)}\">{WebUtility.HtmlEncode(text)}</a></p>";

    private static LawyerEmailNotificationModel RequireLawyer(EmailNotificationModel model)
        => model as LawyerEmailNotificationModel
           ?? throw new ArgumentException("A lawyer notification model is required.", nameof(model));

    private static ConsultationEmailNotificationModel RequireConsultation(EmailNotificationModel model)
        => model as ConsultationEmailNotificationModel
           ?? throw new ArgumentException("A consultation notification model is required.", nameof(model));

    private static ClientEmailNotificationModel RequireClient(EmailNotificationModel model)
        => model as ClientEmailNotificationModel
           ?? throw new ArgumentException("A client notification model is required.", nameof(model));

    private static RegistrationWelcomeEmailNotificationModel RequireRegistration(EmailNotificationModel model)
        => model as RegistrationWelcomeEmailNotificationModel
           ?? throw new ArgumentException("A registration welcome notification model is required.", nameof(model));

    private static ContactInquiryEmailNotificationModel RequireContactInquiry(EmailNotificationModel model)
        => model as ContactInquiryEmailNotificationModel
           ?? throw new ArgumentException("A contact inquiry notification model is required.", nameof(model));

    private static string RequireReason(LawyerEmailNotificationModel model)
        => !string.IsNullOrWhiteSpace(model.Reason)
            ? model.Reason
            : throw new ArgumentException("A reason is required for this notification.", nameof(model));

    private static string RequireReason(ConsultationEmailNotificationModel model)
        => !string.IsNullOrWhiteSpace(model.Reason)
            ? model.Reason
            : throw new ArgumentException("A reason is required for this notification.", nameof(model));
}
