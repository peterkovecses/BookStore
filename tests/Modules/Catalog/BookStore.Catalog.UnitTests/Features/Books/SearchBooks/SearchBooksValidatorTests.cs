namespace BookStore.Catalog.UnitTests.Features.Books.SearchBooks;

public class SearchBooksValidatorTests
{
    private readonly SearchBooksValidator _sut = new();

    [Fact]
    public void Validate_WhenValidRequest_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter(), new PaginationParams(1, 15), new SortingParams(null, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenPageNumberIsInvalid_ShouldHaveError(int pageNumber)
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter(), new PaginationParams(pageNumber, 10), new SortingParams(null, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(query => query.Pagination.PageNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(IPagination.MaxPageSize + 1)]
    public void Validate_WhenPageSizeIsInvalid_ShouldHaveError(int pageSize)
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter(), new PaginationParams(1, pageSize), new SortingParams(null, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(query => query.Pagination.PageSize);
    }

    [Fact]
    public void Validate_WhenFilterIsNull_ShouldHaveError()
    {
        // Arrange
        var request = new SearchBooksQuery(null!, new PaginationParams(1, 15), new SortingParams(null, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(query => query.Filter);
    }

    [Fact]
    public void Validate_WhenSearchTermTooLong_ShouldHaveError()
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter { SearchTerm = new string('a', 101) }, new PaginationParams(1, 15), new SortingParams(null, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(query => query.Filter.SearchTerm);
    }
}
