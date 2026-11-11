using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace BookStore.Identity.IntegrationTests.Verification;

public sealed class VerificationEndpointIntegrationTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    private const string ConfirmationPath = "/api/identity/verification/confirmation";
    private const string ResendPath = "/api/identity/verification/resend";
    private const string Password = "Password1!";
    private const string FirstName = "Jane";
    private const string LastName = "Doe";

    [Fact]
    public async Task VerificationEndpoints_WhenPermitLimitIsReached_ShouldReturnTooManyRequests()
    {
        // Arrange
        const HttpStatusCode expectedAllowedStatus = HttpStatusCode.BadRequest;
        const HttpStatusCode expectedRejectedStatus = HttpStatusCode.TooManyRequests;
        await using var configuredFactory = CreateFactory(
            confirmationPermitLimit: 1,
            resendPermitLimit: 1);
        using var sut = configuredFactory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        using var firstConfirmation = await sut.PostAsJsonAsync(ConfirmationPath, new
        {
            UserId = "unknown-user",
            Token = "valid-shape-token"
        }, cancellationToken);
        using var secondConfirmation = await sut.PostAsJsonAsync(ConfirmationPath, new
        {
            UserId = "unknown-user",
            Token = "valid-shape-token"
        }, cancellationToken);
        using var firstResend = await sut.PostAsJsonAsync(ResendPath, new { Email = "unknown@example.com" }, cancellationToken);
        using var secondResend = await sut.PostAsJsonAsync(ResendPath, new { Email = "unknown@example.com" }, cancellationToken);

        // Assert
        firstConfirmation.StatusCode.Should().Be(expectedAllowedStatus);
        secondConfirmation.StatusCode.Should().Be(expectedRejectedStatus);
        firstResend.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResend.StatusCode.Should().Be(expectedRejectedStatus);
    }

    [Fact]
    public async Task Resend_WhenAccountIsUnknownConfirmedOrUnconfirmed_ShouldReturnSameResponse()
    {
        // Arrange
        const string unknownEmail = "unknown-account@example.com";
        const HttpStatusCode expectedStatus = HttpStatusCode.NoContent;
        await using var configuredFactory = CreateFactory(
            confirmationPermitLimit: 100,
            resendPermitLimit: 100);
        using var sut = configuredFactory.CreateClient();
        using var scope = configuredFactory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var unconfirmedEmail = $"{Guid.NewGuid():N}@example.com";
        var confirmedEmail = $"{Guid.NewGuid():N}@example.com";
        var unconfirmedUser = CreateUser(unconfirmedEmail);
        var confirmedUser = CreateUser(confirmedEmail);
        confirmedUser.EmailConfirmed = true;
        (await users.CreateAsync(unconfirmedUser, Password)).Succeeded.Should().BeTrue();
        (await users.CreateAsync(confirmedUser, Password)).Succeeded.Should().BeTrue();
        var initialOutboxCount = await context.OutboxMessages.CountAsync(TestContext.Current.CancellationToken);

        // Act
        using var unknownResponse = await sut.PostAsJsonAsync(ResendPath, new { Email = unknownEmail }, TestContext.Current.CancellationToken);
        using var confirmedResponse = await sut.PostAsJsonAsync(ResendPath, new { Email = confirmedEmail }, TestContext.Current.CancellationToken);
        using var unconfirmedResponse = await sut.PostAsJsonAsync(ResendPath, new { Email = unconfirmedEmail }, TestContext.Current.CancellationToken);
        var unknownBody = await unknownResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var confirmedBody = await confirmedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var unconfirmedBody = await unconfirmedResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var actualOutboxCount = await context.OutboxMessages.CountAsync(TestContext.Current.CancellationToken);

        // Assert
        unknownResponse.StatusCode.Should().Be(expectedStatus);
        confirmedResponse.StatusCode.Should().Be(expectedStatus);
        unconfirmedResponse.StatusCode.Should().Be(expectedStatus);
        confirmedBody.Should().Be(unknownBody).And.Be(unconfirmedBody);
        actualOutboxCount.Should().Be(initialOutboxCount + 1);
    }

    [Fact]
    public async Task Confirm_WhenIdentityTokenIsInvalid_ShouldRejectAndLeaveAccountUnconfirmed()
    {
        // Arrange
        const string invalidToken = "not-a-valid-identity-token";
        await using var configuredFactory = CreateFactory(confirmationPermitLimit: 100, resendPermitLimit: 100);
        using var sut = configuredFactory.CreateClient();
        using var scope = configuredFactory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = CreateUser(email);
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(invalidToken));

        // Act
        using var actual = await sut.PostAsJsonAsync(ConfirmationPath, new { UserId = user.Id, Token = encodedToken },
            TestContext.Current.CancellationToken);
        var actualBody = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var actualUser = await context.Users.SingleAsync(item => item.Id == user.Id, TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        actualBody.Should().NotContain(invalidToken).And.NotContain(encodedToken);
        actualUser.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task Confirm_WhenIdentityTokenIsTampered_ShouldRejectAndLeaveAccountUnconfirmed()
    {
        // Arrange
        await using var configuredFactory = CreateFactory(confirmationPermitLimit: 100, resendPermitLimit: 100);
        using var sut = configuredFactory.CreateClient();
        using var scope = configuredFactory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = CreateUser(email);
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        var validToken = await users.GenerateEmailConfirmationTokenAsync(user);
        var tamperedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(validToken + "tampered"));

        // Act
        using var actual = await sut.PostAsJsonAsync(ConfirmationPath, new { UserId = user.Id, Token = tamperedToken },
            TestContext.Current.CancellationToken);
        var actualBody = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var actualUser = await context.Users.SingleAsync(item => item.Id == user.Id, TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        actualBody.Should().NotContain(validToken).And.NotContain(tamperedToken);
        actualUser.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task Confirm_WhenIdentityTokenHasExpired_ShouldRejectAndLeaveAccountUnconfirmed()
    {
        // Arrange
        await using var configuredFactory = CreateFactory(
            confirmationPermitLimit: 100,
            resendPermitLimit: 100,
            tokenLifetime: "00:00:01");
        using var sut = configuredFactory.CreateClient();
        using var scope = configuredFactory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = CreateUser(email);
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);

        // Act
        using var actual = await sut.PostAsJsonAsync(ConfirmationPath, new { UserId = user.Id, Token = encodedToken },
            TestContext.Current.CancellationToken);
        var actualBody = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var actualUser = await context.Users.SingleAsync(item => item.Id == user.Id, TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        actualBody.Should().NotContain(token).And.NotContain(encodedToken);
        actualUser.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task Confirm_WhenIdentityTokenIsValidAndAlreadyUsed_ShouldSucceedIdempotently()
    {
        // Arrange
        await using var configuredFactory = CreateFactory(confirmationPermitLimit: 100, resendPermitLimit: 100);
        using var sut = configuredFactory.CreateClient();
        using var scope = configuredFactory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = CreateUser(email);
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var request = new { UserId = user.Id, Token = encodedToken };

        // Act
        using var firstConfirmation = await sut.PostAsJsonAsync(ConfirmationPath, request, TestContext.Current.CancellationToken);
        using var repeatedConfirmation = await sut.PostAsJsonAsync(ConfirmationPath, request, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var actualUser = await context.Users.SingleAsync(item => item.Id == user.Id, TestContext.Current.CancellationToken);

        // Assert
        firstConfirmation.StatusCode.Should().Be(HttpStatusCode.NoContent);
        repeatedConfirmation.StatusCode.Should().Be(HttpStatusCode.NoContent);
        actualUser.EmailConfirmed.Should().BeTrue();
    }

    private WebApplicationFactory<Program> CreateFactory(int confirmationPermitLimit, int resendPermitLimit, string tokenLifetime = "1.00:00:00") =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Identity:Verification:TokenLifetime", tokenLifetime);
            builder.UseSetting("Identity:Verification:RateLimiting:Confirmation:PermitLimit", confirmationPermitLimit.ToString());
            builder.UseSetting("Identity:Verification:RateLimiting:Confirmation:WindowSeconds", "60");
            builder.UseSetting("Identity:Verification:RateLimiting:Resend:PermitLimit", resendPermitLimit.ToString());
            builder.UseSetting("Identity:Verification:RateLimiting:Resend:WindowSeconds", "60");
        });

    private static ApplicationUser CreateUser(string email) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Email = email,
        UserName = email,
        FirstName = FirstName,
        LastName = LastName
    };
}
