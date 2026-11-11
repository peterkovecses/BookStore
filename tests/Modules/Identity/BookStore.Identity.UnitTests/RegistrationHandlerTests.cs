namespace BookStore.Identity.UnitTests;

public class RegistrationHandlerTests
{
    [Fact]
    public void ToValidationResult_WhenIdentityReturnsMixedErrors_ShouldReturnOnlyNonDuplicateErrors()
    {
        // Arrange
        const string expectedMessage = "Password is not acceptable.";
        const string duplicateMessage = "Sensitive duplicate detail";
        var errors = new[]
        {
            new IdentityError { Code = nameof(IdentityErrorDescriber.DuplicateUserName), Description = duplicateMessage },
            new IdentityError { Code = "PasswordRejected", Description = expectedMessage }
        };

        // Act
        var actual = RegistrationHandler.ToValidationResult(errors);

        // Assert
        actual.Should().BeFailure().HaveError("Validation").HaveValidationProperty("Registration");
        actual.Errors!.SelectMany(error => error.Metadata!.Values)
            .OfType<List<string>>().SelectMany(messages => messages)
            .Should().ContainSingle().Which.Should().Be(expectedMessage);
    }
}
