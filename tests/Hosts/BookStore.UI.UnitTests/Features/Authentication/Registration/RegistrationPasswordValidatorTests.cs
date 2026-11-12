namespace BookStore.UI.UnitTests.Features.Authentication.Registration;

public class RegistrationPasswordValidatorTests
{
    [Theory]
    [InlineData("aaaaaaaaaaaa", false)]
    [InlineData("abcabcabcabc", true)]
    [InlineData("abc", false)]
    [InlineData("Password1!", false)]
    public void Validate_WhenPolicyIsNonDefault_ShouldApplyConfiguredLengthAndUniqueCharacters(string password, bool expected)
    {
        // Arrange
        var policy = new PasswordPolicy
        {
            SchemaVersion = 1, RequiredLength = 12, RequiredUniqueChars = 3,
            RequireDigit = false, RequireLowercase = false, RequireUppercase = false, RequireNonAlphanumeric = false
        };
        var sut = new RegistrationPasswordValidator(policy);
        var model = new RegistrationModel { Password = password };

        // Act
        var actual = sut.Validate(model);

        // Assert
        actual.IsValid.Should().Be(expected);
    }
}
