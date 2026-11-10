namespace BookStore.Identity.Infrastructure;

internal sealed class ApplicationUser : IdentityUser
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}
