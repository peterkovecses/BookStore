namespace BookStore.Catalog.UnitTests.Features.Books.SearchBooks;

public class SearchBooksValidatorTests
{
    private readonly SearchBooksValidator _sut = new();

    [Fact]
    public void Validate_WhenValidRequest_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Default, ISorting.Create(null, false));

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
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Create(pageNumber, 10), ISorting.Create(null, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(nameof(SearchBooksQuery.Pagination.PageNumber));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(IPagination.MaxPageSize + 1)]
    public void Validate_WhenPageSizeIsInvalid_ShouldHaveError(int pageSize)
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Create(1, pageSize), ISorting.Create(null, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(nameof(SearchBooksQuery.Pagination.PageSize));
    }

    [Fact]
    public void Validate_WhenFilterIsNull_ShouldHaveError()
    {
        // Arrange
        var request = new SearchBooksQuery(null!, IPagination.Default, ISorting.Create(null, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(query => query.Filter);
    }

    [Fact]
    public void Validate_WhenSearchTermTooLong_ShouldHaveError()
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter { SearchTerm = new string('a', 101) }, IPagination.Default, ISorting.Create(null, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(nameof(SearchBooksFilter.SearchTerm));
    }

    [Theory]
    [InlineData("Title")]
    [InlineData("Price")]
    [InlineData("PublishedOn")]
    [InlineData("AverageRating")]
    public void Validate_WhenSortByIsValid_ShouldNotHaveAnyErrors(string sortBy)
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Default, ISorting.Create(sortBy, false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldNotHaveAnyValidationErrors();
    }


    [Fact]
    public void Validate_WhenSortByIsInvalid_ShouldHaveError()
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Default, ISorting.Create("InvalidField", false));

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(nameof(SearchBooksQuery.Sorting.SortBy));
    }
}
