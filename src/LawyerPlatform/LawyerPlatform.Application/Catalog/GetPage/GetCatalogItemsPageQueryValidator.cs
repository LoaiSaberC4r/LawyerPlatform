using FluentValidation;

namespace LawyerPlatform.Application.Catalog.GetPage;

internal sealed class GetCatalogItemsPageQueryValidator : AbstractValidator<GetCatalogItemsPageQuery>
{
    public GetCatalogItemsPageQueryValidator()
    {
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
        RuleFor(query => query.SearchText).MaximumLength(200);
    }
}
