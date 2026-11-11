using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace BookStore.Identity.UnitTests;

public class VerificationRequestFactoryTests
{
    [Fact]
    public async Task CreateAsync_WhenTokenContainsReservedCharacters_ShouldProtectAndEncodeLink()
    {
        // Arrange
        const string token = "token/+&=secret";
        const string userId = "user+/=";
        const string email = "customer@example.com";
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var users = Substitute.For<UserManager<ApplicationUser>>(store, null!, null!, null!, null!, null!, null!, null!, null!);
        var user = new ApplicationUser { Id = userId, Email = email, FirstName = "Jane", LastName = "Doe" };
        users.GenerateEmailConfirmationTokenAsync(user).Returns(token);
        var protection = new EphemeralDataProtectionProvider();
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);
        var options = Options.Create(new VerificationOptions
        {
            LinkBaseUrl = "https://frontend.example/verify",
            TokenLifetime = TimeSpan.FromHours(2)
        });
        var sut = new VerificationRequestFactory(users, protection, options, clock);

        // Act
        var actual = await sut.CreateAsync(user, TestContext.Current.CancellationToken);
        var value = JsonSerializer.Deserialize<VerificationEmailRequested>(actual.Payload)!;
        var link = protection.CreateProtector(VerificationRequestFactory.ProtectionPurpose)
            .CreateProtector(userId).CreateProtector(email).Unprotect(value.ProtectedLink);
        var query = QueryHelpers.ParseQuery(new Uri(link).Query);
        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(query["token"].ToString()));

        // Assert
        actual.Payload.Should().NotContain(token).And.NotContain(link);
        actual.Id.ToString().Should().NotBe(userId);
        value.UserId.Should().Be(userId);
        value.IssuedAt.Should().Be(now);
        value.ExpiresAt.Should().Be(now + options.Value.TokenLifetime);
        query["userId"].ToString().Should().Be(userId);
        decodedToken.Should().Be(token);
    }

    [Fact]
    public void IsExpired_WhenAtOrAfterDeadline_ShouldReturnTrue()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var value = new VerificationEmailRequested("user", "customer@example.com", "Jane", "protected", now)
        {
            IssuedAt = now.AddHours(-1)
        };
        var sut = new VerificationRequestExpiry(Options.Create(new VerificationOptions()));

        // Act
        var actual = sut.IsExpired(value, now);

        // Assert
        actual.Should().BeTrue();
        sut.IsExpired(value, now.AddTicks(-1)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenConfiguredLifetimeIsShortened_ShouldExpireAtCurrentDeadline()
    {
        // Arrange
        var issuedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var value = new VerificationEmailRequested("user", "customer@example.com", "Jane", "protected", issuedAt.AddDays(1))
        {
            IssuedAt = issuedAt
        };
        var sut = new VerificationRequestExpiry(Options.Create(new VerificationOptions { TokenLifetime = TimeSpan.FromHours(1) }));

        // Act
        var actual = sut.IsExpired(value, issuedAt.AddHours(1));

        // Assert
        actual.Should().BeTrue();
    }
}
