namespace BookStore.Identity.UnitTests.PasswordRecovery;

public sealed class PasswordRecoveryExpiryTests
{
    [Theory]
    [InlineData("http://frontend.example/reset")]
    [InlineData("https://frontend.example/reset?next=other")]
    [InlineData("https://frontend.example/reset#fragment")]
    [InlineData("https://user@frontend.example/reset")]
    public void IsValid_WhenDestinationIsNotTrustedHttpsUrl_ShouldReject(string url)
    {
        // Arrange
        var sut = new PasswordRecoveryOptions { LinkBaseUrl = url };

        // Act
        var actual = sut.IsValid();

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenDeadlineOrConfiguredLifetimeIsReached_ShouldExpire()
    {
        // Arrange
        var issued = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var value = new PasswordResetEmailRequested("user", "user@example.com", "Alice", "protected", issued.AddHours(2))
        {
            IssuedAt = issued
        };
        var sut = new PasswordResetRequestExpiry(Options.Create(new PasswordRecoveryOptions { TokenLifetime = TimeSpan.FromHours(1) }));

        // Act
        var before = sut.IsExpired(value, issued.AddHours(1).AddTicks(-1));
        var actual = sut.IsExpired(value, issued.AddHours(1));

        // Assert
        before.Should().BeFalse();
        actual.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenLifetimeIsExtended_ShouldNotExtendRecordedExpiry()
    {
        // Arrange
        var issued = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var value = new PasswordResetEmailRequested("user", "user@example.com", "Alice", "protected", issued.AddHours(1))
        {
            IssuedAt = issued
        };
        var sut = new PasswordResetRequestExpiry(Options.Create(new PasswordRecoveryOptions { TokenLifetime = TimeSpan.FromHours(2) }));

        // Act
        var actual = sut.IsExpired(value, issued.AddHours(1));

        // Assert
        actual.Should().BeTrue();
    }
}
