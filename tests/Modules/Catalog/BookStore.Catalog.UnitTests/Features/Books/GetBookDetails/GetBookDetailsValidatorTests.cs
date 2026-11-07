namespace BookStore.Catalog.UnitTests.Features.Books.GetBookDetails;

public class GetBookDetailsValidatorTests
{
    private readonly GetBookDetailsValidator _sut = new();

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Validate_WhenIdIsPositiveInteger_ShouldNotHaveAnyErrors(int id)
    {
        var result = _sut.TestValidate(new GetBookDetailsQuery(id));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_WhenIdIsInvalid_ShouldHaveError(int id)
    {
        var result = _sut.TestValidate(new GetBookDetailsQuery(id));

        result.ShouldHaveValidationErrorFor(query => query.Id)
            .WithErrorMessage("Id must be a positive integer.");
    }
}
