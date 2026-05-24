namespace BookStore.Catalog.Common.Mappings;

internal static class BookMappings
{
    public static Expression<Func<Book, BookSummaryDto>> ToBookSummary => book => new BookSummaryDto
    {
        Id = book.Id,
        Title = book.Title,
        PublishedOn = book.PublishedOn,
        Publisher = book.Publisher,
        Price = book.Price,
        ImageName = book.ImageName,
        Promotion = book.Promotion == null ? null : new PriceOfferDto
        {
            Id = book.Promotion.Id,
            NewPrice = book.Promotion.NewPrice,
            Text = book.Promotion.Text,
            BookId = book.Promotion.BookId
        },
        Authors = book.AuthorsLink.Select(al => new AuthorDto
        {
            Id = al.AuthorId,
            Name = al.Author.Name
        }),
        Tags = book.Tags.Select(t => new TagDto
        {
            Id = t.Id
        }),
        ReviewCount = book.Reviews.Count,
        AverageRating = book.Reviews.Any() ? book.Reviews.Average(r => r.Stars) : null
    };

    public static Expression<Func<Book, BookDetailsDto>> ToBookDetailsDto => book => new BookDetailsDto
    {
        Id = book.Id,
        Title = book.Title,
        Description = book.Description,
        PublishedOn = book.PublishedOn,
        Publisher = book.Publisher,
        Price = book.Price,
        ImageName = book.ImageName,
        SoftDeleted = book.SoftDeleted,
        Promotion = book.Promotion == null ? null : new PriceOfferDto
        {
            Id = book.Promotion.Id,
            NewPrice = book.Promotion.NewPrice,
            Text = book.Promotion.Text,
            BookId = book.Promotion.BookId
        },
        Authors = book.AuthorsLink.Select(al => new AuthorDto
        {
            Id = al.AuthorId,
            Name = al.Author.Name
        }),
        Tags = book.Tags.Select(t => new TagDto
        {
            Id = t.Id
        }),
        Reviews = book.Reviews.Select(r => new ReviewDto
        {
            Id = r.Id,
            Voter = r.Voter,
            Stars = r.Stars,
            Comment = r.Comment,
            BookId = r.BookId
        }).ToList(),
        AverageRating = book.Reviews.Any() ? book.Reviews.Average(r => r.Stars) : null
    };
}
