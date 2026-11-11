namespace BookStore.Api.UnitTests;

public sealed class DataProtectionConfigurationTests : IDisposable
{
    private const string ApplicationName = "BookStore.Tests";
    private const string Password = "test-only";
    private const string Purpose = "test-purpose";
    private readonly string _directory = Path.Combine(Directory.GetCurrentDirectory(), ".data-protection-keys", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AddApiDataProtection_WhenInstancesShareKeys_ShouldProtectAcrossRestartsAndEncryptKeys()
    {
        // Arrange
        const string expected = "sensitive-payload";
        var certificatePath = CreateCertificate("current");
        var settings = Settings(certificatePath);
        using var first = CreateHost(settings);
        var protectedValue = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect(expected);
        using var second = CreateHost(settings);

        // Act
        var actual = second.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(protectedValue);
        var keyXml = File.ReadAllText(Directory.GetFiles(_directory, "key-*.xml").Single());

        // Assert
        actual.Should().Be(expected);
        keyXml.Should().Contain("encryptedSecret").And.Contain("EncryptedKey").And.NotContain("<masterKey").And.NotContain(expected);
    }

    [Fact]
    public async Task AddApiDataProtection_WhenInstancesShareKeys_ShouldValidateIdentityTokenAcrossInstances()
    {
        // Arrange
        var settings = Settings(CreateCertificate("current"));
        using var first = CreateHost(settings);
        using var second = CreateHost(settings);
        var user = new IdentityUser { Id = "user-id", SecurityStamp = "security-stamp" };
        var token = await first.Services.GetRequiredService<UserManager<IdentityUser>>()
            .GenerateEmailConfirmationTokenAsync(user);

        // Act
        var actual = await second.Services.GetRequiredService<UserManager<IdentityUser>>()
            .VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "EmailConfirmation", token);

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void AddApiDataProtection_WhenCertificateRotates_ShouldReadOldAndNewKeysAfterRestart()
    {
        // Arrange
        const string expected = "pending-message";
        var oldCertificate = CreateCertificate("old");
        var newCertificate = CreateCertificate("new");
        using var first = CreateHost(Settings(oldCertificate));
        first.Services.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(89));
        var oldValue = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect(expected);
        var rotatedSettings = Settings(newCertificate);
        rotatedSettings["DataProtection:PreviousCertificates:0:Path"] = oldCertificate;
        rotatedSettings["DataProtection:PreviousCertificates:0:Password"] = Password;
        using var rotating = CreateHost(rotatedSettings);
        rotating.Services.GetRequiredService<IKeyManager>().CreateNewKey(DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddDays(90));
        using var restarted = CreateHost(rotatedSettings);
        var protector = restarted.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);
        var newValue = protector.Protect(expected);
        using var newCertificateOnly = CreateHost(Settings(newCertificate));

        // Act
        var actualOld = protector.Unprotect(oldValue);
        var actualNew = newCertificateOnly.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose).Unprotect(newValue);

        // Assert
        actualOld.Should().Be(expected);
        actualNew.Should().Be(expected);
        Directory.GetFiles(_directory, "key-*.xml").Should().HaveCount(2);
    }

    [Fact]
    public void AddApiDataProtection_WhenApplicationNameDiffers_ShouldRejectProtectedPayload()
    {
        // Arrange
        var settings = Settings(CreateCertificate("current"));
        using var first = CreateHost(settings);
        var protectedValue = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("payload");
        settings["DataProtection:ApplicationName"] = "different-application";
        using var second = CreateHost(settings);

        // Act
        var act = () => second.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(protectedValue);

        // Assert
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void AddApiDataProtection_WhenProductionCertificateIsMissing_ShouldFailClosed()
    {
        // Arrange
        var settings = Settings(CreateCertificate("current"));
        settings.Remove("DataProtection:Certificate:Path");

        // Act
        var act = () => CreateHost(settings);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddApiDataProtection_WhenDevelopmentCertificateIsMissing_ShouldFailClosed()
    {
        // Arrange
        var settings = Settings(CreateCertificate("current"));
        settings.Remove("DataProtection:Certificate:Path");

        // Act
        var act = () => CreateHost(settings, Environments.Development);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddApiDataProtection_WhenExistingKeyIsUnencrypted_ShouldFailClosed()
    {
        // Arrange
        var settings = Settings(CreateCertificate("current"));
        File.WriteAllText(Path.Combine(_directory, "key-plaintext.xml"), "<key><masterKey>plaintext</masterKey></key>");

        // Act
        var act = () => CreateHost(settings);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddApiDataProtection_WhenPreviousCertificateIsMissing_ShouldNotDecryptOldPayload()
    {
        // Arrange
        var oldCertificate = CreateCertificate("old");
        var newCertificate = CreateCertificate("new");
        using var first = CreateHost(Settings(oldCertificate));
        var protectedValue = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("payload");
        using var second = CreateHost(Settings(newCertificate));

        // Act
        var act = () => second.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Unprotect(protectedValue);

        // Assert
        act.Should().Throw<CryptographicException>();
    }

    private string CreateCertificate(string name)
    {
        Directory.CreateDirectory(_directory);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var path = Path.Combine(_directory, $"{name}.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, Password));

        return path;
    }

    private Dictionary<string, string?> Settings(string certificatePath) => new()
    {
        ["DataProtection:ApplicationName"] = ApplicationName,
        ["DataProtection:KeyRingPath"] = _directory,
        ["DataProtection:Certificate:Path"] = certificatePath,
        ["DataProtection:Certificate:Password"] = Password
    };

    private IHost CreateHost(Dictionary<string, string?> settings, string environment = "Production")
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = environment,
            ContentRootPath = _directory,
            DisableDefaults = true
        });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddApiDataProtection();
        var store = Substitute.For<IUserStore<IdentityUser>, IUserSecurityStampStore<IdentityUser>>();
        store.GetUserIdAsync(Arg.Any<IdentityUser>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<IdentityUser>().Id));
        ((IUserSecurityStampStore<IdentityUser>)store).GetSecurityStampAsync(Arg.Any<IdentityUser>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<IdentityUser>().SecurityStamp));
        builder.Services.AddSingleton(store);
        builder.Services.AddIdentityCore<IdentityUser>().AddDefaultTokenProviders();

        return builder.Build();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
