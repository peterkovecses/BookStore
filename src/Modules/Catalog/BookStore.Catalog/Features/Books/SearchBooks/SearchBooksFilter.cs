namespace BookStore.Catalog.Features.Books.SearchBooks;

internal class SearchBooksFilter : FilterBase<Book>
{
    public string? SearchTerm { get; init; }
    public HashSet<int> AuthorIds { get; init; } = [];
    public HashSet<string> TagIds { get; init; } = [];

    public override ExpressionStarter<Book> GetPredicate()
    {
        var predicate = base.GetPredicate();

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            predicate = predicate.And(b => 
                b.Title.Contains(SearchTerm) || 
                b.Description.Contains(SearchTerm) ||
                b.Publisher.Contains(SearchTerm) ||
                b.AuthorsLink.Any(al => al.Author.Name.Contains(SearchTerm)));
        }

        if (AuthorIds.Count > 0)
        {
            predicate = predicate.And(b => b.AuthorsLink.Any(al => AuthorIds.Contains(al.AuthorId)));
        }

        if (TagIds.Count > 0)
        {
            predicate = predicate.And(b => b.Tags.Any(t => TagIds.Contains(t.Id)));
        }

        return predicate;
    }
}
