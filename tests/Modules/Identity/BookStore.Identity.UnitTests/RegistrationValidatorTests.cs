using BookStore.Identity.Contracts;
using BookStore.Identity.Features.Registration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BookStore.Identity.UnitTests;

public class RegistrationValidatorTests
{
    private readonly RegistrationValidator _validator = new(Options.Create(new IdentityOptions()));

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
    public void InvalidRegistrationIsRejected(string email, string password, string firstName, string lastName)
    {
        var result = _validator.Validate(new RegisterCustomer(new RegistrationRequest
        {
            Email = email, Password = password, FirstName = firstName, LastName = lastName
        }));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidRegistrationIsAccepted()
    {
        var result = _validator.Validate(new RegisterCustomer(new RegistrationRequest
        {
            Email = "jane@example.com", Password = "Password1!", FirstName = "Jane", LastName = "Doe"
        }));

        Assert.True(result.IsValid);
    }
}
