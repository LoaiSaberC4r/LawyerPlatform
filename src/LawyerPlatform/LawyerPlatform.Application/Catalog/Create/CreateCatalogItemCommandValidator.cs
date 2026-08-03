using FluentValidation;

namespace LawyerPlatform.Application.Catalog.Create;

internal sealed class CreateCatalogItemCommandValidator : AbstractValidator<CreateCatalogItemCommand>
{
    public CreateCatalogItemCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(command => command.Description)
            .MaximumLength(2000);

        RuleFor(command => command.Price)
            .GreaterThanOrEqualTo(0);
    }
}
