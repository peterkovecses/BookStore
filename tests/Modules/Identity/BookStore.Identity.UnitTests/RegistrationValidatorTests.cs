using BookStore.Identity.Contracts;
using BookStore.Identity.Features.Registration;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BookStore.Identity.UnitTests;

public class RegistrationValidatorTests
{
    private readonly RegistrationValidator _sut = new(Options.Create(new IdentityOptions()));

    [Theory]
    [InlineData("", "Password1!", "Jane", "Doe")]
    [InlineData("invalid", "Password1!", "Jane", "Doe")]
    [InlineData("jane@example.com", "Password1!", "", "Doe")]
    [InlineData("jane@example.com", "Password1!", "Jane", " ")]
    [InlineData("jane@example.com", "short", "Jane", "Doe")]
    [InlineData("jane@example.com", "password1!", "Jane", "Doe")]
    [InlineData("jane@example.com", "PASSWORD1!", "Jane", "Doe")]
    [InlineData("jane@example.com", "Password!", "Jane", "Doe")]
    [InlineData("jane@example.com", "Password1", "Jane", "Doe")]
    public void Validate_WhenRegistrationIsInvalid_ShouldRejectIt(string email, string password, string firstName, string lastName)
    {
        // Arrange
        var request = new RegisterCommand(new RegistrationRequest
        {
            Email = email, Password = password, FirstName = firstName, LastName = lastName
        });

        // Act
        var actual = _sut.Validate(request);

        // Assert
        actual.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WhenRegistrationIsValid_ShouldAcceptIt()
    {
        // Arrange
        const string email = "jane@example.com";
        const string password = "Password1!";
        const string firstName = "Jane";
        const string lastName = "Doe";
        var request = new RegisterCommand(new RegistrationRequest
        {
            Email = email, Password = password, FirstName = firstName, LastName = lastName
        });

        // Act
        var actual = _sut.Validate(request);

        // Assert
        actual.IsValid.Should().BeTrue();
    }
}
