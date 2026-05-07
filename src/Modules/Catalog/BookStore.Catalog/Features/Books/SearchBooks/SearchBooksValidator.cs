namespace BookStore.Catalog.Features.Books.SearchBooks;

internal class SearchBooksValidator : AbstractValidator<SearchBooksQuery>
{
    private const int MaxSearchTermLength = 100;
    private const int MinPageSize = 1;

    private static readonly HashSet<string> AllowedSortProperties = 
        new(StringComparer.OrdinalIgnoreCase) { "Title", "Price", "PublishedOn" };

    private static readonly string InvalidSortByMessage = 
        $"SortBy must be one of the following: {string.Join(", ", AllowedSortProperties)}";

    public SearchBooksValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(IPagination.MinPageNumber);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(MinPageSize, IPagination.MaxPageSize);

        RuleFor(x => x.SortBy)
            .Must(x => x is null || AllowedSortProperties.Contains(x))
            .WithMessage(InvalidSortByMessage);

        RuleFor(x => x.Filter)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.Filter.SearchTerm)
                    .MaximumLength(MaxSearchTermLength);
            });
    }
}
