namespace BookStore.Catalog.Features.Books.GetBookDetails;

internal sealed class GetBookDetailsValidator : AbstractValidator<GetBookDetailsQuery>
{
    public GetBookDetailsValidator()
    {
        RuleFor(query => query.Id)
            .GreaterThan(0)
            .WithMessage("Id must be a positive integer.");
    }
}
