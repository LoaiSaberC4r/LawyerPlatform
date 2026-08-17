using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Results;
using BuildingBlock.Domain.Specification;
using FluentValidation;
using LawyerPlatform.Application.Features.ConsultationRequests.Create;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.Clients;
using LawyerPlatform.Domain.Consultations;

namespace LawyerPlatform.Application.Features.ConsultationRequests.CreateClient;

public sealed record CreateClientConsultationRequestCommand(
    Guid LawyerId,
    int? LegalSpecializationId,
    string Description,
    DateTime? PreferredAppointmentOnUtc)
    : ICommand<CreateConsultationRequestResponse>, ITransactionalCommand<LawyerPlatformWritePersistence>;

internal sealed class CreateClientConsultationRequestCommandValidator
    : AbstractValidator<CreateClientConsultationRequestCommand>
{
    public CreateClientConsultationRequestCommandValidator()
    {
        RuleFor(command => command.LawyerId).NotEmpty();
        RuleFor(command => command.LegalSpecializationId)
            .GreaterThan(0)
            .When(command => command.LegalSpecializationId.HasValue);
        RuleFor(command => command.Description)
            .NotEmpty()
            .MaximumLength(ConsultationRequest.MaximumDescriptionLength);
    }
}

internal sealed class CreateClientConsultationRequestCommandHandler(
    ICurrentUser currentUser,
    IReadRepository<ClientProfile, LawyerPlatformReadPersistence> clientReader,
    ConsultationRequestCreationService service)
    : ICommandHandler<CreateClientConsultationRequestCommand, CreateConsultationRequestResponse>
{
    public async Task<Result<CreateConsultationRequestResponse>> Handle(
        CreateClientConsultationRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<CreateConsultationRequestResponse>.Fail(
                Error.NotFound("ClientProfile.NotFound", "The current Client profile was not found."));
        }

        var client = await clientReader.FirstOrDefaultAsync(
            new CurrentClientProfileSpecification(userId),
            cancellationToken);
        if (client is null)
        {
            return Result<CreateConsultationRequestResponse>.Fail(
                Error.NotFound("ClientProfile.NotFound", "The current Client profile was not found."));
        }

        return await service.CreateClientAsync(
            client.Id,
            command.LawyerId,
            command.LegalSpecializationId,
            client.FullName,
            client.Email,
            command.Description,
            command.PreferredAppointmentOnUtc,
            cancellationToken);
    }
}

internal sealed record CurrentClientProfileSnapshot(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string Email);

internal sealed class CurrentClientProfileSpecification
    : Specification<ClientProfile, CurrentClientProfileSnapshot>
{
    public CurrentClientProfileSpecification(Guid userAccountId)
    {
        AddCriteria(profile => profile.UserAccountId == userAccountId && !profile.IsDeleted);
        UseNoTracking();
        Select(profile => new CurrentClientProfileSnapshot(
            profile.Id,
            profile.FullName,
            profile.UserAccount.PhoneNumber,
            profile.UserAccount.Email));
    }
}
