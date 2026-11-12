namespace BookStore.UI.UnitTests.Features.Authentication.Registration;

public class RegistrationModelTests
{
    [Theory]
    [InlineData("", "Doe", "jane@example.com", "Password1!")]
    [InlineData("Jane", "", "jane@example.com", "Password1!")]
    [InlineData("Jane", "Doe", "invalid", "Password1!")]
    [InlineData("Jane", "Doe", "jane@example.com", "")]
    public void Validate_WhenInputIsInvalid_ShouldRejectModel(string firstName, string lastName, string email, string password)
    {
        // Arrange
        var sut = new RegistrationModel { FirstName = firstName, LastName = lastName, Email = email, Password = password, ConfirmPassword = password };
        var errors = new List<ValidationResult>();

        // Act
        var actual = Validator.TryValidateObject(sut, new ValidationContext(sut), errors, true);

        // Assert
        actual.Should().BeFalse();
        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_WhenInputIsValid_ShouldAcceptModel()
    {
        // Arrange
        var sut = new RegistrationModel { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", Password = "Password1!", ConfirmPassword = "Password1!" };

        // Act
        var actual = Validator.TryValidateObject(sut, new ValidationContext(sut), [], true);

        // Assert
        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Different1!")]
    public void Validate_WhenConfirmationIsMissingOrDifferent_ShouldReportConfirmation(string confirmation)
    {
        // Arrange
        var sut = new RegistrationModel { FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", Password = "Password1!", ConfirmPassword = confirmation };
        var errors = new List<ValidationResult>();

        // Act
        var actual = Validator.TryValidateObject(sut, new ValidationContext(sut), errors, true);

        // Assert
        actual.Should().BeFalse();
        errors.Should().Contain(error => error.MemberNames.Contains(nameof(RegistrationModel.ConfirmPassword)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    public void Validate_WhenResendEmailIsInvalid_ShouldRejectModel(string email)
    {
        // Arrange
        var sut = new ResendModel { Email = email };

        // Act
        var actual = Validator.TryValidateObject(sut, new ValidationContext(sut), [], true);

        // Assert
        actual.Should().BeFalse();
    }
}
