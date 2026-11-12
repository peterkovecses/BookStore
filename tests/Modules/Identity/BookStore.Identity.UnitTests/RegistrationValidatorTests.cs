namespace BookStore.Identity.UnitTests;

public class RegistrationValidatorTests
{
    private readonly RegistrationValidator _sut = new(Options.Create(new IdentityOptions()));

    [Theory]
    [InlineData("", "Password1!", "Jane", "Doe")]
    [InlineData("invalid", "Password1!", "Jane", "Doe")]
    [InlineData("jane@example.com", "Password1!", "", "Doe")]
    [InlineData("jane@example.com", "Password1!", "Jane", " ")]
    [InlineData("jane@example.com", "short", "Jane", "Doe")]
    [InlineData("jane@example.com", "password1!", "Jane", "Doe")]
    [InlineData("jane@example.com", "PASSWORD1!", "Jane", "Doe")]
    [InlineData("jane@example.com", "Password!", "Jane", "Doe")]
    [InlineData("jane@example.com", "Password1", "Jane", "Doe")]
    public void Validate_WhenRegistrationIsInvalid_ShouldRejectIt(string email, string password, string firstName, string lastName)
    {
        // Arrange
        var request = new RegisterCommand(new RegistrationRequest
        {
            Email = email, Password = password, FirstName = firstName, LastName = lastName
        });

        // Act
        var actual = _sut.Validate(request);

        // Assert
        actual.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenFieldsAreNull_ShouldReportEachField()
    {
        // Arrange
        string[] expected = ["Email", "Password", "FirstName", "LastName"];
        var request = new RegisterCommand(new RegistrationRequest());

        // Act
        var actual = _sut.Validate(request);

        // Assert
        actual.IsValid.Should().BeFalse();
        actual.Errors.Select(error => error.PropertyName).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Validate_WhenRegistrationIsValid_ShouldAcceptIt()
    {
        // Arrange
        const string email = "jane@example.com";
        const string password = "Password1!";
        const string firstName = "Jane";
        const string lastName = "Doe";
        var request = new RegisterCommand(new RegistrationRequest
        {
            Email = email, Password = password, FirstName = firstName, LastName = lastName
        });

        // Act
        var actual = _sut.Validate(request);

        // Assert
        actual.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("aaaaaaaaaaaa", false)]
    [InlineData("abcabcabcabc", true)]
    [InlineData("abc", false)]
    [InlineData("éééééééééééé", false)]
    public void Validate_WhenPolicyIsConfigured_ShouldApplyLengthAndUniqueCharacters(string password, bool expected)
    {
        // Arrange
        var options = new IdentityOptions();
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 3;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        var sut = new RegistrationValidator(Options.Create(options));
        var request = new RegisterCommand(new RegistrationRequest { Email = "jane@example.com", Password = password, FirstName = "Jane", LastName = "Doe" });

        // Act
        var actual = sut.Validate(request);

        // Assert
        actual.IsValid.Should().Be(expected);
    }

    [Theory]
    [InlineData("Äbcdef1!", false)]
    [InlineData("Abcdef١!", false)]
    [InlineData("Abcdef1é", true)]
    [InlineData("Abcdef1!", true)]
    public void Validate_WhenCharactersAreNonAscii_ShouldMatchIdentityAsciiRules(string password, bool expected)
    {
        // Arrange
        var request = new RegisterCommand(new RegistrationRequest { Email = "jane@example.com", Password = password, FirstName = "Jane", LastName = "Doe" });

        // Act
        var actual = _sut.Validate(request);

        // Assert
        actual.IsValid.Should().Be(expected);
    }
}
