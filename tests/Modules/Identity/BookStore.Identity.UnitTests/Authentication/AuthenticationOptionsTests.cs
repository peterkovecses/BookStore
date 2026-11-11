namespace BookStore.Identity.UnitTests.Authentication;

public sealed class AuthenticationOptionsTests
{
    [Fact]
    public void IsValid_WhenSecureConfigurationIsProvided_ShouldReturnTrue()
    {
        // Arrange
        var sut = CreateOptions();

        // Act
        var actual = sut.IsValid();

        // Assert
        actual.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-base64")]
    [InlineData("AQID")]
    public void IsValid_WhenSigningKeyIsInvalid_ShouldReturnFalse(string key)
    {
        // Arrange
        var sut = CreateOptions();
        sut.SigningKey = key;

        // Act
        var actual = sut.IsValid();

        // Assert
        actual.Should().BeFalse();
    }

    [Theory]
    [InlineData("http://ui.example.test")]
    [InlineData("https://ui.example.test/path")]
    [InlineData("*")]
    [InlineData("https://user@ui.example.test")]
    public void IsValid_WhenOriginIsUnsafe_ShouldReturnFalse(string origin)
    {
        // Arrange
        var sut = CreateOptions();
        sut.AllowedOrigins = [origin];

        // Act
        var actual = sut.IsValid();

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenTokenLifetimesAreUnsafe_ShouldReturnFalse()
    {
        // Arrange
        var sut = CreateOptions();
        sut.AccessTokenLifetime = TimeSpan.FromMinutes(16);

        // Act
        var actual = sut.IsValid();

        // Assert
        actual.Should().BeFalse();
    }

    private static AuthenticationOptions CreateOptions() => new()
    {
        Issuer = "https://api.example.test",
        Audience = "bookstore-test",
        SigningKey = Convert.ToBase64String(new byte[32]),
        AllowedOrigins = ["https://ui.example.test"]
    };
}
