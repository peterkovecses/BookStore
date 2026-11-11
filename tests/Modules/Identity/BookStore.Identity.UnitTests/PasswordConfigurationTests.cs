namespace BookStore.Identity.UnitTests;

public class PasswordConfigurationTests
{
    [Fact]
    public async Task AddIdentityModuleServices_WhenPasswordPolicyIsConfigured_ShouldApplyItToIdentity()
    {
        // Arrange
        const int expectedLength = 12;
        const int expectedUniqueCharacters = 3;
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Identity:Password:RequiredLength"] = "12",
            ["Identity:Password:RequiredUniqueChars"] = "3",
            ["Identity:Password:RequireDigit"] = "false",
            ["Identity:Password:RequireLowercase"] = "false",
            ["Identity:Password:RequireUppercase"] = "false",
            ["Identity:Password:RequireNonAlphanumeric"] = "false"
        });
        using var sut = builder.Build();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        using var scope = sut.Services.CreateScope();
        var actual = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().Options.Password;

        // Assert
        actual.RequiredLength.Should().Be(expectedLength);
        actual.RequiredUniqueChars.Should().Be(expectedUniqueCharacters);
        actual.RequireDigit.Should().BeFalse();
        actual.RequireLowercase.Should().BeFalse();
        actual.RequireUppercase.Should().BeFalse();
        actual.RequireNonAlphanumeric.Should().BeFalse();
    }

    [Theory]
    [InlineData("Identity:Password:RequiredLength", "5")]
    [InlineData("Identity:Password:RequiredUniqueChars", "0")]
    public async Task StartAsync_WhenPasswordPolicyIsInvalid_ShouldThrowOptionsValidationException(string key, string value)
    {
        // Arrange
        var builder = CreateBuilder(new Dictionary<string, string?> { [key] = value });
        using var sut = builder.Build();

        // Act
        var act = () => sut.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task StartAsync_WhenAuthenticationSigningKeyIsInvalid_ShouldThrowOptionsValidationException()
    {
        // Arrange
        var builder = CreateBuilder(new Dictionary<string, string?>());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:Authentication:SigningKey"] = "invalid"
        });
        using var sut = builder.Build();

        // Act
        var act = () => sut.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task StartAsync_WhenAuthenticationAndVerificationOptionsAreConfigured_ShouldBindInitOnlyProperties()
    {
        // Arrange
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Identity:Authentication:AccessTokenLifetime"] = "00:05:00",
            ["Identity:Authentication:CookieSameSite"] = "Lax",
            ["Identity:Authentication:LoginPermitLimit"] = "7",
            ["Identity:Verification:TokenLifetime"] = "02:00:00"
        });
        using var sut = builder.Build();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var authentication = sut.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var verification = sut.Services.GetRequiredService<IOptions<VerificationOptions>>().Value;

        // Assert
        authentication.AccessTokenLifetime.Should().Be(TimeSpan.FromMinutes(5));
        authentication.CookieSameSite.Should().Be("Lax");
        authentication.LoginPermitLimit.Should().Be(7);
        verification.LinkBaseUrl.Should().Be("https://localhost/verify-email");
        verification.TokenLifetime.Should().Be(TimeSpan.FromHours(2));
    }

    private static HostApplicationBuilder CreateBuilder(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();
        settings["ConnectionStrings:BookStoreDb"] = "Server=localhost;Database=BookStore;Trusted_Connection=True";
        settings["Identity:Verification:LinkBaseUrl"] = "https://localhost/verify-email";
        settings["Identity:PasswordRecovery:LinkBaseUrl"] = "https://localhost/reset-password";
        settings["Identity:Authentication:Issuer"] = "https://api.example.test";
        settings["Identity:Authentication:Audience"] = "bookstore-test";
        settings["Identity:Authentication:SigningKey"] = Convert.ToBase64String(new byte[32]);
        settings["Identity:Authentication:AllowedOrigins:0"] = "https://ui.example.test";
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.AddIdentityModuleServices();

        return builder;
    }
}
