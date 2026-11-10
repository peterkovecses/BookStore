using BookStore.Identity.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BookStore.Identity.UnitTests;

public class PasswordConfigurationTests
{
    [Fact]
    public async Task ConfiguredPasswordOptionsAreUsedByIdentity()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["Identity:Password:RequiredLength"] = "12",
            ["Identity:Password:RequiredUniqueChars"] = "3",
            ["Identity:Password:RequireDigit"] = "false",
            ["Identity:Password:RequireLowercase"] = "false",
            ["Identity:Password:RequireUppercase"] = "false",
            ["Identity:Password:RequireNonAlphanumeric"] = "false"
        });
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        using var scope = host.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().Options.Password;
        Assert.Equal(12, options.RequiredLength);
        Assert.Equal(3, options.RequiredUniqueChars);
        Assert.False(options.RequireDigit);
        Assert.False(options.RequireLowercase);
        Assert.False(options.RequireUppercase);
        Assert.False(options.RequireNonAlphanumeric);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("Identity:Password:RequiredLength", "5")]
    [InlineData("Identity:Password:RequiredUniqueChars", "0")]
    public async Task InvalidPolicyFailsAtStartup(string key, string value)
    {
        var builder = CreateBuilder(new Dictionary<string, string?> { [key] = value });
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync(TestContext.Current.CancellationToken));
        Assert.Contains(key, exception.Message);
    }

    private static HostApplicationBuilder CreateBuilder(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();
        settings["ConnectionStrings:BookStoreDb"] = "Server=localhost;Database=BookStore;Trusted_Connection=True";
        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddIdentityModuleServices();

        return builder;
    }
}
