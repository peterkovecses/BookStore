using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BookStore.Testing.Common.Integration;

/// <summary>
/// Universal base WebApplicationFactory for integration testing.
/// Handles environment configuration, authentication, and core service overrides.
/// </summary>
/// <typeparam name="TMarker">Assembly marker interface for API project identification</typeparam>
public abstract class BaseWebApplicationFactory<TMarker> : WebApplicationFactory<TMarker>, IAsyncLifetime
    where TMarker : class
{
    private readonly string _dataProtectionDirectory = Path.Combine(
        Directory.GetCurrentDirectory(), ".data-protection-keys", Guid.NewGuid().ToString("N"));
    private readonly Lock _dataProtectionLock = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var certificatePath = Path.Combine(_dataProtectionDirectory, "test-certificate.pfx");
        lock (_dataProtectionLock)
        {
            Directory.CreateDirectory(_dataProtectionDirectory);
            if (!File.Exists(certificatePath))
            {
                using var rsa = RSA.Create(2048);
                var request = new CertificateRequest("CN=BookStore integration test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
                File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pkcs12, "test-only"));
            }
        }

        builder.UseEnvironment("IntegrationTesting");
        builder.UseSetting("DataProtection:ApplicationName", "BookStore.Api.IntegrationTesting");
        builder.UseSetting("DataProtection:KeyRingPath", _dataProtectionDirectory);
        builder.UseSetting("DataProtection:Certificate:Path", certificatePath);
        builder.UseSetting("DataProtection:Certificate:Password", "test-only");
        builder.UseSetting("Identity:Verification:LinkBaseUrl", "https://localhost/verify-email");
        builder.UseSetting("EmailDispatcher:Smtp:Host", "localhost");
        builder.UseSetting("EmailDispatcher:Smtp:Username", "test-user");
        builder.UseSetting("EmailDispatcher:Smtp:Password", "test-password");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureTestServices(services =>
        {
            ConfigureDefaultServices(services);
            ConfigureAuthentication(services);
            ConfigureCustomServices(services);
        });
    }

    /// <summary>
    /// Default service configuration (removing hosted services).
    /// Override if you need different default behavior.
    /// </summary>
    protected virtual void ConfigureDefaultServices(IServiceCollection services) => services.RemoveAll<IHostedService>();

    /// <summary>
    /// Configure JWT authentication. Concrete implementations can override for custom config.
    /// </summary>
    protected virtual void ConfigureAuthentication(IServiceCollection services)
    {
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var key = Encoding.UTF8.GetBytes(JwtConstants.SecurityKey);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };
        });
    }

    /// <summary>
    /// Configure custom services specific to the concrete implementation.
    /// </summary>
    protected virtual void ConfigureCustomServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Initialize the test factory.
    /// </summary>
    public virtual ValueTask InitializeAsync()
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Cleanup resources.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        catch (ObjectDisposedException)
        {
            // Ignore ObjectDisposedException on host disposal due to physical file provider watcher issues in some environments.
        }

        if (Directory.Exists(_dataProtectionDirectory))
        {
            Directory.Delete(_dataProtectionDirectory, recursive: true);
        }
    }
}