namespace BookStore.Catalog.UnitTests.Features.Books.GetBookDetails;

public class GetBookDetailsValidatorTests
{
    private readonly GetBookDetailsValidator _sut = new();

    [Theory]
    [InlineData("1")]
    [InlineData("2147483647")]
    public void Validate_WhenIdIsPositiveInteger_ShouldNotHaveAnyErrors(string id)
    {
        var result = _sut.TestValidate(new GetBookDetailsQuery(id));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("not-an-integer")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenIdIsInvalid_ShouldHaveError(string? id)
    {
        var result = _sut.TestValidate(new GetBookDetailsQuery(id!));

        result.ShouldHaveValidationErrorFor(query => query.Id)
            .WithErrorMessage("Id must be a positive integer.");
    }
}
