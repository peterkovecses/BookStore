namespace BookStore.Catalog.Features.Books.SearchBooks;

internal class SearchBooksValidator : AbstractValidator<SearchBooksQuery>
{
    private const int MaxSearchTermLength = 100;
    private const int MinPageSize = 1;

    public SearchBooksValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(IPagination.MinPageNumber);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(MinPageSize, IPagination.MaxPageSize);

        RuleFor(x => x.Filter)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.Filter.SearchTerm)
                    .MaximumLength(MaxSearchTermLength);
            });
    }
}
