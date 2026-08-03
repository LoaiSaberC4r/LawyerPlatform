using FluentValidation;

namespace LawyerPlatform.Application.Catalog.Update;

internal sealed class UpdateCatalogItemCommandValidator : AbstractValidator<UpdateCatalogItemCommand>
{
    public UpdateCatalogItemCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Description).MaximumLength(2000);
        RuleFor(command => command.Price).GreaterThanOrEqualTo(0);
    }
}
