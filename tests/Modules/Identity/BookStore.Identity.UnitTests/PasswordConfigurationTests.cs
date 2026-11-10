using BookStore.Identity.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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

    private static HostApplicationBuilder CreateBuilder(Dictionary<string, string?> settings)
    {
        var builder = Host.CreateApplicationBuilder();
        settings["ConnectionStrings:BookStoreDb"] = "Server=localhost;Database=BookStore;Trusted_Connection=True";
        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddIdentityModuleServices();

        return builder;
    }
}
