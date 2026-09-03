namespace LawyerPlatform.Infrastructure.Persistence;

internal static class LawyerPlatformDatabaseObjectNames
{
    public const string Schema = "dbo";

    public const string ConsultationRequestReferenceNumberUniqueIndex =
        "UX_ConsultationRequests_ReferenceNumber";

    public const string LawyerAvailabilityUniqueIndex =
        "UX_LawyerAvailabilities_SettingsId_Type_DayOfWeek";

    public const string GovernorateIdSequence = "SEQ_Governorates_Id";
    public const string CityIdSequence = "SEQ_Cities_Id";
    public const string AreaIdSequence = "SEQ_Areas_Id";
    public const string LegalSpecializationIdSequence = "SEQ_LegalSpecializations_Id";

    public const int GovernorateIdSequenceStart = 10_000;
    public const int CityIdSequenceStart = 100_000;
    public const int AreaIdSequenceStart = 1_000_000_000;
    public const int LegalSpecializationIdSequenceStart = 10_000;
}
