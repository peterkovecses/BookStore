namespace BookStore.EmailDispatcher.IntegrationTests;

public sealed class SmtpOptionsStartupTests
{
    [Theory]
    [InlineData("", "sender@example.com")]
    [InlineData("localhost", "")]
    [InlineData("localhost", "invalid address")]
    public async Task StartAsync_WhenRequiredSettingsAreMissingOrInvalid_ShouldFail(string hostName, string fromAddress)
    {
        // Arrange
        var builder = CreateBuilder(hostName, fromAddress);
        using var sut = builder.Build();

        // Act
        var act = () => sut.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task StartAsync_WhenValidSettingsAreConfigured_ShouldBindInitOnlyValues()
    {
        // Arrange
        const string hostName = "smtp.example.com";
        const string fromAddress = "sender@example.com";
        const int expectedPort = 2525;
        const int expectedTimeout = 12;
        var builder = CreateBuilder(hostName, fromAddress);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmailDispatcher:Smtp:Port"] = "2525",
            ["EmailDispatcher:Smtp:OperationTimeoutSeconds"] = "12"
        });
        using var sut = builder.Build();

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var actual = sut.Services.GetRequiredService<IOptions<SmtpSettings>>().Value;

        // Assert
        actual.Host.Should().Be(hostName);
        actual.FromAddress.Should().Be(fromAddress);
        actual.Port.Should().Be(expectedPort);
        actual.OperationTimeoutSeconds.Should().Be(expectedTimeout);
        actual.Security.Should().Be(SecureSocketOptions.StartTls);
        await sut.StopAsync(TestContext.Current.CancellationToken);
    }

    private static HostApplicationBuilder CreateBuilder(string hostName, string fromAddress)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmailDispatcher:Smtp:Host"] = hostName,
            ["EmailDispatcher:Smtp:FromAddress"] = fromAddress
        });
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.AddEmailDispatcherModuleServices();

        return builder;
    }
}
