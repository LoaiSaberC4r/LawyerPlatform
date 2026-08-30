using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using LawyerPlatform.Application.Abstractions.ContactInquiries;
using LawyerPlatform.Application.Common.Validation;
using LawyerPlatform.Application.Notifications.Email;
using LawyerPlatform.Application.Persistence;
using LawyerPlatform.Domain.ContactInquiries;
using LawyerPlatform.Domain.Resources;

namespace LawyerPlatform.Application.Features.ContactInquiries.Create;

public sealed record CreateContactInquiryCommand(
    string FullName,
    string PhoneNumber,
    string Email,
    string InquiryType,
    string Message)
    : ICommand<CreateContactInquiryResponse>,
      ITransactionalCommand<LawyerPlatformWritePersistence>;

public sealed record CreateContactInquiryResponse(Guid Id, DateTime CreatedOnUtc);

internal sealed class CreateContactInquiryCommandValidator
    : AbstractValidator<CreateContactInquiryCommand>
{
    public CreateContactInquiryCommandValidator()
    {
        RuleFor(command => command.FullName)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode("ContactInquiry.FullNameRequired")
            .WithMessage(_ => ErrorMessage.FullNameRequired)
            .Must(value => value.Trim().Length <= ContactInquiry.MaximumFullNameLength)
            .WithErrorCode("ContactInquiry.FullNameTooLong")
            .WithMessage(_ => ErrorMessage.FullNameTooLong);

        RuleFor(command => command.PhoneNumber)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode("ContactInquiry.PhoneNumberRequired")
            .WithMessage(_ => ErrorMessage.PhoneNumberRequired)
            .EgyptianMobileNumber()
            .WithErrorCode("ContactInquiry.PhoneNumberInvalid")
            .WithMessage(_ => ErrorMessage.PhoneNumberInvalid);

        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode("ContactInquiry.EmailRequired")
            .WithMessage(_ => ErrorMessage.EmailRequired)
            .MaximumLength(ContactInquiry.MaximumEmailLength)
            .WithErrorCode("ContactInquiry.EmailInvalid")
            .WithMessage(_ => ErrorMessage.EmailInvalid)
            .EmailAddress()
            .WithErrorCode("ContactInquiry.EmailInvalid")
            .WithMessage(_ => ErrorMessage.EmailInvalid);

        RuleFor(command => command.InquiryType)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode("ContactInquiry.InquiryTypeRequired")
            .WithMessage(_ => ErrorMessage.InquiryTypeRequired)
            .Must(value => value.Trim().Length <= ContactInquiry.MaximumInquiryTypeLength)
            .WithErrorCode("ContactInquiry.InquiryTypeTooLong")
            .WithMessage(_ => ErrorMessage.InquiryTypeTooLong);

        RuleFor(command => command.Message)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrorCode("ContactInquiry.MessageRequired")
            .WithMessage(_ => ErrorMessage.MessageRequired)
            .Must(value => value.Trim().Length <= ContactInquiry.MaximumMessageLength)
            .WithErrorCode("ContactInquiry.MessageTooLong")
            .WithMessage(_ => ErrorMessage.MessageTooLong);
    }
}

internal sealed class CreateContactInquiryCommandHandler(
    IUnitOfWork<LawyerPlatformWritePersistence> unitOfWork,
    IDateTimeProvider clock,
    IContactUsRecipientProvider recipientProvider,
    EmailNotificationCoordinator emailNotifications)
    : ICommandHandler<CreateContactInquiryCommand, CreateContactInquiryResponse>
{
    public async Task<Result<CreateContactInquiryResponse>> Handle(
        CreateContactInquiryCommand command,
        CancellationToken cancellationToken)
    {
        var inquiryResult = ContactInquiry.Create(
            command.FullName,
            command.PhoneNumber,
            command.Email,
            command.InquiryType,
            command.Message,
            clock.UtcNow);
        if (inquiryResult.IsFailure)
        {
            return Result<CreateContactInquiryResponse>.Fail(inquiryResult.Errors);
        }

        var inquiry = inquiryResult.Value;
        await unitOfWork.WriteRepository<ContactInquiry>()
            .AddAsync(inquiry, cancellationToken);
        await emailNotifications.QueueContactInquiryAsync(
            inquiry,
            recipientProvider.SupportEmail,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreateContactInquiryResponse>.Ok(
            new CreateContactInquiryResponse(inquiry.Id, inquiry.CreatedOnUtc));
    }
}
