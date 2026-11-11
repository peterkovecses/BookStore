namespace BookStore.Identity.UnitTests.Verification;

public class VerificationResendRequestedHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenUserIsUnknown_ShouldNotCreateVerificationEmail()
    {
        // Arrange
        const string email = "unknown@example.com";
        var users = CreateUsers();
        users.FindByEmailAsync(email).Returns((ApplicationUser?)null);
        var requests = Substitute.For<IVerificationRequestFactory>();
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer().Options);
        var protection = new EphemeralDataProtectionProvider();
        var sut = new VerificationResendRequestedHandler(users, requests, context, protection);
        var value = new VerificationResendRequested(
            protection.CreateProtector(VerificationResendRequested.ProtectionPurpose).Protect(email));

        // Act
        await sut.HandleAsync(value, TestContext.Current.CancellationToken);

        // Assert
        await users.Received(1).FindByEmailAsync(email);
        await requests.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
        context.ChangeTracker.Entries<OutboxMessage>().Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsConfirmed_ShouldNotCreateVerificationEmail()
    {
        // Arrange
        const string email = "confirmed@example.com";
        var user = new ApplicationUser { Email = email, FirstName = "Jane", LastName = "Doe", EmailConfirmed = true };
        var users = CreateUsers();
        users.FindByEmailAsync(email).Returns(user);
        users.IsEmailConfirmedAsync(user).Returns(true);
        var requests = Substitute.For<IVerificationRequestFactory>();
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer().Options);
        var protection = new EphemeralDataProtectionProvider();
        var sut = new VerificationResendRequestedHandler(users, requests, context, protection);
        var value = new VerificationResendRequested(
            protection.CreateProtector(VerificationResendRequested.ProtectionPurpose).Protect(email));

        // Act
        await sut.HandleAsync(value, TestContext.Current.CancellationToken);

        // Assert
        await users.Received(1).IsEmailConfirmedAsync(user);
        await requests.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
        context.ChangeTracker.Entries<OutboxMessage>().Should().BeEmpty();
    }

    private static UserManager<ApplicationUser> CreateUsers() =>
        Substitute.For<UserManager<ApplicationUser>>(Substitute.For<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
}
