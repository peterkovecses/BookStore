namespace BookStore.Catalog.UnitTests.Features.Books.SearchBooks;

public class SearchBooksValidatorTests
{
    private readonly SearchBooksValidator _sut = new();

    [Fact]
    public void Validate_WhenValidRequest_ShouldNotHaveAnyErrors()
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter(), 1, 10, null, false);

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
        var request = new SearchBooksQuery(new SearchBooksFilter(), pageNumber, 10, null, false);

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(query => query.PageNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_WhenPageSizeIsInvalid_ShouldHaveError(int pageSize)
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter(), 1, pageSize, null, false);

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(query => query.PageSize);
    }

    [Fact]
    public void Validate_WhenFilterIsNull_ShouldHaveError()
    {
        // Arrange
        var request = new SearchBooksQuery(null!, 1, 10, null, false);

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(query => query.Filter);
    }

    [Fact]
    public void Validate_WhenSearchTermTooLong_ShouldHaveError()
    {
        // Arrange
        var request = new SearchBooksQuery(new SearchBooksFilter { SearchTerm = new string('a', 101) }, 1, 10, null, false);

        // Act
        var actual = _sut.TestValidate(request);

        // Assert
        actual.ShouldHaveValidationErrorFor(query => query.Filter.SearchTerm);
    }
}
