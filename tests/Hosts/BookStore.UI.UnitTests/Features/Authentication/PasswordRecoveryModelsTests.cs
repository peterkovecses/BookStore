namespace BookStore.UI.UnitTests.Features.Authentication;

public class PasswordRecoveryModelsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_WhenForgotPasswordEmailIsInvalid_ShouldRejectModel(string email)
    {
        // Arrange
        var sut = new ForgotPasswordModel { Email = email };

        // Act
        var actual = IsValid(sut);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenForgotPasswordEmailIsValid_ShouldAcceptModel()
    {
        // Arrange
        var sut = new ForgotPasswordModel { Email = "jane@example.com" };

        // Act
        var actual = IsValid(sut);

        // Assert
        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Password1!")]
    [InlineData("short", "short")]
    [InlineData("password1!", "password1!")]
    [InlineData("Password!!", "Password!!")]
    [InlineData("Password1!", "Different1!")]
    public void Validate_WhenResetPasswordIsInvalid_ShouldRejectModel(string password, string confirmation)
    {
        // Arrange
        var sut = new PasswordResetModel { NewPassword = password, ConfirmPassword = confirmation };

        // Act
        var actual = IsValid(sut);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenResetPasswordMeetsPolicyAndMatches_ShouldAcceptModel()
    {
        // Arrange
        const string password = "Password1!";
        var sut = new PasswordResetModel { NewPassword = password, ConfirmPassword = password };

        // Act
        var actual = IsValid(sut);

        // Assert
        actual.Should().BeTrue();
    }

    private static bool IsValid(object model) =>
        Validator.TryValidateObject(model, new ValidationContext(model), [], true);
}
