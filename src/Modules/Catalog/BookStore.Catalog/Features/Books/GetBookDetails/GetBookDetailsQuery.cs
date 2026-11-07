namespace BookStore.Catalog.Features.Books.GetBookDetails;

internal record GetBookDetailsQuery(string Id) : IRequest<Result<BookDetailsDto>>;
