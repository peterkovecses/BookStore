namespace BookStore.Identity.UnitTests;

public class RegistrationHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenIdentityReturnsMixedErrors_ShouldReturnOnlyNonDuplicateErrors()
    {
        // Arrange
        const string expectedMessage = "Password is not acceptable.";
        const string duplicateMessage = "Sensitive duplicate detail";
        const string password = "Password1!";
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var users = Substitute.For<UserManager<ApplicationUser>>(store, null!, null!, null!, null!, null!, null!, null!, null!);
        users.CreateAsync(Arg.Any<ApplicationUser>(), password).Returns(IdentityResult.Failed(
            new IdentityError { Code = nameof(IdentityErrorDescriber.DuplicateUserName), Description = duplicateMessage },
            new IdentityError { Code = "PasswordRejected", Description = expectedMessage }));
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer().Options;
        using var context = new IdentityDbContext(options);
        var ids = Substitute.For<IUserIdGenerator>();
        var sut = new RegistrationHandler(users, context, ids);
        var request = new RegisterCommand(new RegistrationRequest
        {
            Email = "customer@example.com", Password = password, FirstName = "Jane", LastName = "Doe"
        });

        // Act
        var actual = await sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeFailure().HaveError("Validation").HaveValidationProperty("Registration");
        actual.Errors!.SelectMany(error => error.Metadata!.Values)
            .OfType<List<string>>().SelectMany(messages => messages)
            .Should().ContainSingle().Which.Should().Be(expectedMessage);
    }
}
