namespace BookStore.Catalog.UnitTests.Features.Books.GetBookDetails;

public class GetBookDetailsValidatorTests
{
    private readonly GetBookDetailsValidator _sut = new();

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Validate_WhenIdIsPositiveInteger_ShouldNotHaveAnyErrors(int id)
    {
        // Arrange
        var request = new GetBookDetailsQuery(id);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_WhenIdIsInvalid_ShouldHaveError(int id)
    {
        // Arrange
        var request = new GetBookDetailsQuery(id);

        // Act
        var result = _sut.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(query => query.Id)
            .WithErrorMessage("Id must be a positive integer.");
    }
}
