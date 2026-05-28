namespace BookStore.UI.Features.Books.Models;

public class BookSearchParameters
{
    public string? SearchTerm { get; set; }
    public HashSet<int> AuthorIds { get; set; } = [];
    public HashSet<string> TagIds { get; set; } = [];

    public void Clear()
    {
        SearchTerm = null;
        AuthorIds.Clear();
        TagIds.Clear();
    }
}
