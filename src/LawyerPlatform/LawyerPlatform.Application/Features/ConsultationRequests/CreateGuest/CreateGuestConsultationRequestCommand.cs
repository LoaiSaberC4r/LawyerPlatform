using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Common.Validation;
using LawyerPlatform.Application.Features.ConsultationRequests.Create;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.CreateGuest;

public sealed record CreateGuestConsultationRequestCommand(
    Guid LawyerId,
    int? LegalSpecializationId,
    string FullName,
    string PhoneNumber,
    string? Email,
    string Description,
    DateTime? PreferredAppointmentOnUtc)
    : ICommand<CreateConsultationRequestResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class CreateGuestConsultationRequestCommandValidator
    : AbstractValidator<CreateGuestConsultationRequestCommand>
{
    public CreateGuestConsultationRequestCommandValidator()
    {
        RuleFor(command => command.LawyerId).NotEmpty();
        RuleFor(command => command.LegalSpecializationId)
            .GreaterThan(0)
            .When(command => command.LegalSpecializationId.HasValue);
        RuleFor(command => command.FullName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("ConsultationRequest.PhoneNumberRequired")
            .EgyptianMobileNumber().WithErrorCode("ConsultationRequest.InvalidPhoneNumber");
        RuleFor(command => command.Email)
            .MaximumLength(320)
            .EmailAddress()
            .When(command => !string.IsNullOrWhiteSpace(command.Email));
        RuleFor(command => command.Description)
            .NotEmpty()
            .MaximumLength(ConsultationRequest.MaximumDescriptionLength);
    }
}

internal sealed class CreateGuestConsultationRequestCommandHandler(
    ConsultationRequestCreationService service)
    : ICommandHandler<CreateGuestConsultationRequestCommand, CreateConsultationRequestResponse>
{
    public Task<Result<CreateConsultationRequestResponse>> Handle(
        CreateGuestConsultationRequestCommand command,
        CancellationToken cancellationToken)
        => service.CreateGuestAsync(
            command.LawyerId,
            command.LegalSpecializationId,
            command.FullName,
            command.PhoneNumber,
            command.Email,
            command.Description,
            command.PreferredAppointmentOnUtc,
            cancellationToken);
}
