namespace BookStore.Identity.Testing.Common;

public sealed class IdentityOutboxTestApplicationFactory : BaseWebApplicationFactory<Program>
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;
    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:latest")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(HttpPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(SmtpPort))
        .Build();

    public Uri MailpitAddress => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(HttpPort)}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:BookStoreDb", _database.GetConnectionString());
        builder.UseSetting("ConnectionStrings:book-covers", "UseDevelopmentStorage=true");
        builder.UseSetting("EmailDispatcher:Smtp:Host", _mailpit.Hostname);
        builder.UseSetting("EmailDispatcher:Smtp:Port", _mailpit.GetMappedPublicPort(SmtpPort).ToString());
        builder.UseSetting("EmailDispatcher:Smtp:Security", "None");
        builder.UseSetting("EmailDispatcher:Smtp:Username", "");
        builder.UseSetting("EmailDispatcher:Smtp:Password", "");
        builder.UseSetting("EmailDispatcher:Smtp:FromAddress", "sender@example.com");
        builder.UseSetting("EmailDispatcher:Smtp:MaxAttempts", "1");
        builder.UseSetting("EmailDispatcher:Smtp:CircuitFailureThreshold", "1");
        builder.UseSetting("EmailDispatcher:Smtp:CircuitBreakSeconds", "5");
        builder.UseSetting("Identity:Verification:TokenLifetime", "00:00:20");
        builder.UseSetting("Outbox:PollInterval", "00:00:00.100");
        builder.ConfigureServices(services =>
        {
            // Mailpit is a local, unauthenticated test sink; retain the production validator with a development environment.
            var descriptor = services.Single(service =>
                service.ImplementationType?.FullName == "BookStore.EmailDispatcher.Infrastructure.Smtp.SmtpSettingsValidator");
            services.Remove(descriptor);
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(Environments.Development);
            services.AddSingleton(descriptor.ServiceType, Activator.CreateInstance(descriptor.ImplementationType!, environment)!);
        });
    }

    public override async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        await _mailpit.StartAsync();
        await base.InitializeAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            try
            {
                await _mailpit.DisposeAsync();
            }
            finally
            {
                await _database.DisposeAsync();
            }
        }
    }
}
