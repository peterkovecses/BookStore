namespace BookStore.Catalog.Features.Books.GetBookDetails;

internal record GetBookDetailsQuery(int Id) : IRequest<Result<BookDetailsDto>>;
