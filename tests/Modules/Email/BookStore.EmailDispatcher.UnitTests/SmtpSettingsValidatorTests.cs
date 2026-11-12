namespace BookStore.EmailDispatcher.UnitTests;

public sealed class SmtpSettingsValidatorTests
{
    [Theory]
    [InlineData(SecureSocketOptions.None)]
    [InlineData(SecureSocketOptions.Auto)]
    [InlineData(SecureSocketOptions.StartTlsWhenAvailable)]
    public void Validate_WhenProductionSecurityIsInsecure_ShouldFail(SecureSocketOptions security)
    {
        // Arrange
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        var sut = new SmtpSettingsValidator(environment);
        var settings = new SmtpSettings { Host = "smtp.example.com", FromAddress = "sender@example.com", Username = "user", Password = "password", Security = security };

        // Act
        var actual = sut.Validate(null, settings);

        // Assert
        actual.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenProductionUsesTlsAndCredentials_ShouldSucceed()
    {
        // Arrange
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        var sut = new SmtpSettingsValidator(environment);
        var settings = new SmtpSettings { Host = "smtp.example.com", FromAddress = "sender@example.com", Username = "user", Password = "password" };

        // Act
        var actual = sut.Validate(null, settings);

        // Assert
        actual.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenDevelopmentHasNoCredentials_ShouldSucceed()
    {
        // Arrange
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        var sut = new SmtpSettingsValidator(environment);
        var settings = new SmtpSettings { Host = "localhost", FromAddress = "sender@example.com", Security = SecureSocketOptions.None };

        // Act
        var actual = sut.Validate(null, settings);

        // Assert
        actual.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenMissingHostOrAuthentication_ShouldFail()
    {
        // Arrange
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        var sut = new SmtpSettingsValidator(environment);
        var settings = new SmtpSettings { Host = "", FromAddress = "sender@example.com", Username = "user" };

        // Act
        var actual = sut.Validate(null, settings);

        // Assert
        actual.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenLimitsAreInvalid_ShouldFail()
    {
        // Arrange
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        var sut = new SmtpSettingsValidator(environment);
        var settings = new SmtpSettings { Host = "localhost", FromAddress = "sender@example.com", Port = 0, MaxAttempts = 0, OperationTimeoutSeconds = 0 };

        // Act
        var actual = sut.Validate(null, settings);

        // Assert
        actual.Failed.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid address")]
    public void Validate_WhenSenderIsMissingOrInvalid_ShouldFail(string fromAddress)
    {
        // Arrange
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        var sut = new SmtpSettingsValidator(environment);
        var settings = new SmtpSettings { Host = "localhost", FromAddress = fromAddress };

        // Act
        var actual = sut.Validate(null, settings);

        // Assert
        actual.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_WhenProductionHasNoCredentials_ShouldFail()
    {
        // Arrange
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        var sut = new SmtpSettingsValidator(environment);
        var settings = new SmtpSettings { Host = "smtp.example.com", FromAddress = "sender@example.com" };

        // Act
        var actual = sut.Validate(null, settings);

        // Assert
        actual.Failed.Should().BeTrue();
    }
}
