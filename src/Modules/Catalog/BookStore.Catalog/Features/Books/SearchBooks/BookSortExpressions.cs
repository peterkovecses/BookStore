namespace BookStore.Catalog.Features.Books.SearchBooks;

internal static class BookSortExpressions
{
    public static readonly IReadOnlyDictionary<string, LambdaExpression> Mappings =
        new Dictionary<string, LambdaExpression>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = (Expression<Func<Book, string>>)(b => b.Title),
            
            ["PublishedOn"] = (Expression<Func<Book, DateTime>>)(b => b.PublishedOn),

            ["Price"] = (Expression<Func<Book, decimal>>)(b =>
                b.Promotion != null ? b.Promotion.NewPrice : b.Price),

            ["AverageRating"] = (Expression<Func<Book, double?>>)(b =>
                b.Reviews.Any() ? (double?)b.Reviews.Average(r => (double)r.Stars) : null)
        };
}
