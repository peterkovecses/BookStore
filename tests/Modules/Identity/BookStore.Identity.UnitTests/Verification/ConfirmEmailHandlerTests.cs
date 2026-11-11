namespace BookStore.Identity.UnitTests.Verification;

public class ConfirmEmailHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenTokenIsValid_ShouldConfirmEmail()
    {
        // Arrange
        const string userId = "customer-1";
        const string token = "identity-confirmation-token-å";
        var user = new ApplicationUser { Id = userId, FirstName = "Jane", LastName = "Doe" };
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var users = CreateUserManager(store);
        users.FindByIdAsync(userId).Returns(user);
        users.IsEmailConfirmedAsync(user).Returns(false);
        users.ConfirmEmailAsync(user, token).Returns(IdentityResult.Success);
        var sut = new ConfirmEmailHandler(users);
        var request = new ConfirmEmailCommand(new ConfirmationRequest
        {
            UserId = userId,
            Token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
        });

        // Act
        var actual = await sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSuccess();
        await users.Received(1).ConfirmEmailAsync(user, token);
    }

    [Fact]
    public async Task HandleAsync_WhenTokenIsInvalid_ShouldReturnGenericFailureWithoutConfirming()
    {
        // Arrange
        const string userId = "customer-1";
        const string token = "invalid-identity-token";
        var user = new ApplicationUser { Id = userId, FirstName = "Jane", LastName = "Doe" };
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var users = CreateUserManager(store);
        users.FindByIdAsync(userId).Returns(user);
        users.IsEmailConfirmedAsync(user).Returns(false);
        users.ConfirmEmailAsync(user, token).Returns(IdentityResult.Failed(
            new IdentityError { Code = "InvalidToken", Description = "Sensitive identity detail." }));
        var sut = new ConfirmEmailHandler(users);
        var request = new ConfirmEmailCommand(new ConfirmationRequest
        {
            UserId = userId,
            Token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
        });

        // Act
        var actual = await sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeFailure().HaveError("EmailConfirmation");
        var errorMetadataValues = actual.Errors!
            .Where(error => error.Metadata is not null)
            .SelectMany(error => error.Metadata!.Values)
            .OfType<string>();
        errorMetadataValues.Should().NotContain(token);
    }

    [Fact]
    public async Task HandleAsync_WhenConfirmationWasAlreadyCompleted_ShouldReturnSuccessWithoutRevalidatingToken()
    {
        // Arrange
        const string userId = "customer-1";
        var user = new ApplicationUser { Id = userId, FirstName = "Jane", LastName = "Doe" };
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var users = CreateUserManager(store);
        users.FindByIdAsync(userId).Returns(user);
        users.IsEmailConfirmedAsync(user).Returns(true);
        var sut = new ConfirmEmailHandler(users);
        var request = new ConfirmEmailCommand(new ConfirmationRequest { UserId = userId, Token = "expired-or-replayed-token" });

        // Act
        var actual = await sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSuccess();
        await users.DidNotReceive().ConfirmEmailAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_WhenTokenIsNotBase64Url_ShouldReturnGenericFailure()
    {
        // Arrange
        const string userId = "customer-1";
        const string token = "not%base64url";
        var user = new ApplicationUser { Id = userId, FirstName = "Jane", LastName = "Doe" };
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var users = CreateUserManager(store);
        users.FindByIdAsync(userId).Returns(user);
        users.IsEmailConfirmedAsync(user).Returns(false);
        var sut = new ConfirmEmailHandler(users);
        var request = new ConfirmEmailCommand(new ConfirmationRequest { UserId = userId, Token = token });

        // Act
        var actual = await sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeFailure().HaveError("EmailConfirmation");
        await users.DidNotReceive().ConfirmEmailAsync(Arg.Any<ApplicationUser>(), Arg.Any<string>());
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ShouldReturnGenericFailure()
    {
        // Arrange
        const string userId = "unknown-customer";
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var users = CreateUserManager(store);
        users.FindByIdAsync(userId).Returns((ApplicationUser?)null);
        var sut = new ConfirmEmailHandler(users);
        var request = new ConfirmEmailCommand(new ConfirmationRequest { UserId = userId, Token = "valid-shape-token" });

        // Act
        var actual = await sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeFailure().HaveError("EmailConfirmation");
    }

    private static UserManager<ApplicationUser> CreateUserManager(IUserStore<ApplicationUser> store) =>
        Substitute.For<UserManager<ApplicationUser>>(store, null!, null!, null!, null!, null!, null!, null!, null!);
}
