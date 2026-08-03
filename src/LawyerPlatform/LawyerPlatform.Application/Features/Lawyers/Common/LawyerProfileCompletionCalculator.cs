using LawyerPlatform.Application.Abstractions.Lawyers;
using LawyerPlatform.Domain.Lawyers;

namespace LawyerPlatform.Application.Features.Lawyers.Common;

internal static class LawyerProfileCompletionCalculator
{
    public static LawyerProfileCompletionResponse Calculate(
        LawyerApprovalStatus approvalStatus,
        string fullName,
        string? professionalTitle,
        int? yearsOfExperience,
        string? professionalRegistrationNumber,
        bool officeComplete,
        bool specializationsComplete,
        IReadOnlyCollection<string> activeDocumentTypes,
        bool accountIsActive,
        ILawyerDocumentPolicy documentPolicy)
    {
        var professionalComplete =
            !string.IsNullOrWhiteSpace(fullName) &&
            !string.IsNullOrWhiteSpace(professionalTitle) &&
            yearsOfExperience is >= 0 &&
            !string.IsNullOrWhiteSpace(professionalRegistrationNumber);

        var requirementsConfigured = documentPolicy.RequiredDocumentTypes.Count > 0;
        var documentsComplete = requirementsConfigured &&
            documentPolicy.RequiredDocumentTypes.All(required =>
                activeDocumentTypes.Contains(required, StringComparer.OrdinalIgnoreCase));
        var profileComplete = professionalComplete && officeComplete && specializationsComplete && documentsComplete;

        var missing = new List<string>(5);
        if (!professionalComplete)
        {
            missing.Add("ProfessionalProfile");
        }

        if (!officeComplete)
        {
            missing.Add("PrimaryOffice");
        }

        if (!specializationsComplete)
        {
            missing.Add("LegalSpecializations");
        }

        if (!requirementsConfigured)
        {
            missing.Add("DocumentRequirementsConfiguration");
        }
        else if (!documentsComplete)
        {
            missing.Add("RequiredDocuments");
        }

        return new LawyerProfileCompletionResponse(
            professionalComplete,
            officeComplete,
            specializationsComplete,
            documentsComplete,
            profileComplete,
            accountIsActive && profileComplete && approvalStatus is LawyerApprovalStatus.Draft or LawyerApprovalStatus.ChangesRequested,
            missing);
    }
}
