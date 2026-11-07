using System.Globalization;

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
        var bookId = int.Parse(request.Id, CultureInfo.InvariantCulture);

        var book = await dbContext.Books
            .AsNoTracking()
            .Where(book => book.Id == bookId)
            .Where(book => !book.SoftDeleted)
            .Select(BookMappings.ToBookDetailsDto)
            .FirstOrDefaultAsync(cancellationToken);

        if (book is null)
        {
            return Result.Failure<BookDetailsDto>(
                Error.NotFound(message: $"Book with id {bookId} was not found."));
        }

        if (!string.IsNullOrWhiteSpace(book.ImageName))
        {
            book.ImageUrl = storageService.GetPublicUrl(book.ImageName);
        }

        return Result.Success(book);
    }
}
