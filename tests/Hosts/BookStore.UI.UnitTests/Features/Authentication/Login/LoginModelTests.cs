namespace BookStore.UI.UnitTests.Features.Authentication.Login;

public class LoginModelTests
{
    [Theory]
    [InlineData("", "password")]
    [InlineData("invalid", "password")]
    [InlineData("customer@example.com", "")]
    public void Validate_WhenInputIsInvalid_ShouldRejectModel(string email, string password)
    {
        // Arrange
        var sut = new LoginModel { Email = email, Password = password };
        var results = new List<ValidationResult>();

        // Act
        var actual = Validator.TryValidateObject(sut, new ValidationContext(sut), results, true);

        // Assert
        actual.Should().BeFalse();
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_WhenInputIsValid_ShouldAcceptModel()
    {
        // Arrange
        var sut = new LoginModel { Email = "customer@example.com", Password = "password" };

        // Act
        var actual = Validator.TryValidateObject(sut, new ValidationContext(sut), [], true);

        // Assert
        actual.Should().BeTrue();
    }
}
