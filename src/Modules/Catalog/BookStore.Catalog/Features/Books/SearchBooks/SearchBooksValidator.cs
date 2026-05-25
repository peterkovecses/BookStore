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
        RuleFor(x => x.Pagination.PageNumber)
            .GreaterThanOrEqualTo(IPagination.MinPageNumber)
            .OverridePropertyName(nameof(IPagination.PageNumber));

        RuleFor(x => x.Pagination.PageSize)
            .InclusiveBetween(MinPageSize, IPagination.MaxPageSize)
            .OverridePropertyName(nameof(IPagination.PageSize));

        RuleFor(x => x.Sorting.SortBy)
            .Must(x => x is null || AllowedSortProperties.Contains(x))
            .WithMessage(InvalidSortByMessage)
            .OverridePropertyName(nameof(ISorting.SortBy));

        RuleFor(x => x.Filter)
            .NotNull()
            .DependentRules(() =>
            {
                RuleFor(x => x.Filter.SearchTerm)
                    .MaximumLength(MaxSearchTermLength);
            });
    }
}
