using LawyerPlatform.Application.Features.ConsultationRequests.CreateClient;
using LawyerPlatform.Application.Features.ConsultationRequests.CreateGuest;
using LawyerPlatform.Application.Features.Lawyers.ConsultationSettings.Update;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.UnitTests.Consultations;

public sealed class ConsultationTypeValidatorTests
{
    [Fact]
    public void GuestAndClientCreationRequireKnownConsultationType()
    {
        var guest = new CreateGuestConsultationRequestCommandValidator().Validate(
            new CreateGuestConsultationRequestCommand(
                Guid.NewGuid(),
                null,
                1,
                "Guest",
                "01012345678",
                "guest@example.test",
                "Description",
                null));
        var client = new CreateClientConsultationRequestCommandValidator().Validate(
            new CreateClientConsultationRequestCommand(
                Guid.NewGuid(),
                (ConsultationType)999,
                1,
                "Description",
                null));

        Assert.Contains(guest.Errors, error =>
            error.PropertyName == "ConsultationType" &&
            error.ErrorCode == "ConsultationRequest.InvalidConsultationType");
        Assert.Contains(client.Errors, error =>
            error.PropertyName == "ConsultationType" &&
            error.ErrorCode == "ConsultationRequest.InvalidConsultationType");
    }

    [Fact]
    public void SettingsUpdateRequiresBothGroupsAndTheirAvailabilityCollections()
    {
        var validator = new UpdateLawyerConsultationSettingsCommandValidator();
        var missingGroups = validator.Validate(
            new UpdateLawyerConsultationSettingsCommand(null, null, null));
        var missingAvailability = validator.Validate(
            new UpdateLawyerConsultationSettingsCommand(
                new UpdateLawyerOnlineConsultationSettings(500m, null),
                new UpdateLawyerOnsiteConsultationSettings(null),
                null));

        Assert.Contains(missingGroups.Errors, error =>
            error.ErrorCode == "Lawyer.OnlineConsultationSettingsRequired");
        Assert.Contains(missingGroups.Errors, error =>
            error.ErrorCode == "Lawyer.OnsiteConsultationSettingsRequired");
        Assert.Equal(2, missingAvailability.Errors.Count(error =>
            error.ErrorCode == "Lawyer.AvailabilityInvalid"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    public void SettingsUpdateRejectsInvalidOnlinePrice(decimal price)
    {
        var result = new UpdateLawyerConsultationSettingsCommandValidator().Validate(
            new UpdateLawyerConsultationSettingsCommand(
                new UpdateLawyerOnlineConsultationSettings(price, []),
                new UpdateLawyerOnsiteConsultationSettings([]),
                null));

        Assert.Contains(result.Errors, error =>
            error.ErrorCode == "Lawyer.ConsultationPriceInvalid");
    }
}
