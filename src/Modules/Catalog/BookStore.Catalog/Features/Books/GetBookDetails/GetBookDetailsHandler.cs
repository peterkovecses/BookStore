namespace BookStore.Catalog.Features.Books.GetBookDetails;

internal sealed class GetBookDetailsHandler(
    ICatalogDbContext dbContext,
    ICatalogStorageService storageService)
    : IRequestHandler<GetBookDetailsQuery, Result<BookDetailsDto>>
{
    public async Task<Result<BookDetailsDto>> HandleAsync(
        GetBookDetailsQuery request,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .AsNoTracking()
            .Where(book => book.Id == request.Id)
            .Where(book => !book.SoftDeleted)
            .Select(BookMappings.ToBookDetailsDto)
            .FirstOrDefaultAsync(cancellationToken);

        if (book is null)
        {
            return Result.Failure<BookDetailsDto>(
                Error.NotFound($"Book with id {request.Id} was not found."));
        }

        if (book.ImageName is not null)
        {
            book.ImageUrl = storageService.GetPublicUrl(book.ImageName);
        }

        return Result.Success(book);
    }
}
