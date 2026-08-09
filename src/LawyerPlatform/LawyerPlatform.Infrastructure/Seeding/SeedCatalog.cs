namespace LawyerPlatform.Infrastructure.Seeding;

internal sealed record GovernorateSeed(int Id, string NameAr, string NameEn, int DisplayOrder);
internal sealed record CitySeed(int Id, int GovernorateId, string NameAr, string NameEn, int DisplayOrder);
internal sealed record AreaSeed(int Id, int CityId, string NameAr, string NameEn, int DisplayOrder);
internal sealed record LegalSpecializationSeed(int Id, string NameAr, string NameEn, int DisplayOrder);

internal static class SeedCatalog
{
    public const int ExpectedLegalSpecializationCount = 18;

    public static IReadOnlyList<GovernorateSeed> Governorates => EgyptLocationSeedCatalog.Governorates;
    public static IReadOnlyList<CitySeed> Cities => EgyptLocationSeedCatalog.Cities;
    public static IReadOnlyList<AreaSeed> Areas => EgyptLocationSeedCatalog.Areas;
    public static IReadOnlyList<LegalSpecializationSeed> LegalSpecializations { get; } =
    [
        new(1, "قضايا الأسرة والأحوال الشخصية", "Family and Personal Status Law", 10),
        new(2, "المواريث والتركات", "Inheritance and Estate Law", 20),
        new(3, "القضايا الجنائية", "Criminal Law", 30),
        new(4, "القضايا المدنية والتعويضات", "Civil Law and Compensation", 40),
        new(5, "العقارات والإيجارات", "Real Estate and Tenancy Law", 50),
        new(6, "القانون التجاري والشركات", "Commercial and Corporate Law", 60),
        new(7, "تأسيس الشركات والاستثمار", "Company Formation and Investment Law", 70),
        new(8, "القضايا الاقتصادية والمصرفية", "Economic and Banking Law", 80),
        new(9, "قضايا العمل والتأمينات الاجتماعية", "Labor and Social Insurance Law", 90),
        new(10, "القضاء الإداري ومجلس الدولة", "Administrative and State Council Law", 100),
        new(11, "الضرائب والجمارك", "Tax and Customs Law", 110),
        new(12, "العقود والصياغة القانونية", "Contracts and Legal Drafting", 120),
        new(13, "التحكيم وتسوية المنازعات", "Arbitration and Dispute Resolution", 130),
        new(14, "الملكية الفكرية", "Intellectual Property Law", 140),
        new(15, "جرائم تقنية المعلومات والجرائم الإلكترونية", "Information Technology and Cybercrime Law", 150),
        new(16, "القانون الدولي وشؤون الأجانب", "International Law and Foreigners Affairs", 160),
        new(17, "القانون البحري والجوي", "Maritime and Aviation Law", 170),
        new(18, "المرافعات والتنفيذ", "Litigation and Enforcement", 180)
    ];
}
