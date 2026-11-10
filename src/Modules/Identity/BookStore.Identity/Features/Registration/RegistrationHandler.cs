using Microsoft.Data.SqlClient;

namespace BookStore.Identity.Features.Registration;

internal sealed class RegistrationHandler(
    UserManager<ApplicationUser> users,
    IdentityDbContext context,
    IUserIdGenerator idGenerator)
    : IRequestHandler<RegisterCommand, Result>
{
    public async Task<Result> HandleAsync(RegisterCommand request, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = idGenerator.Create(),
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        try
        {
            var result = await users.CreateAsync(user, request.Password);
            if (result.Succeeded || (result.Errors.Any() && result.Errors.All(error =>
                    error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName))))
            {
                return Result.Success();
            }

            var metadata = result.Errors.GroupBy(_ => "Registration")
                .ToDictionary(group => group.Key, group => (object)group.Select(error => error.Description).ToList());

            return Result.Failure(Error.Validation("Validation", "Validation failed.", metadata));
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            context.Entry(user).State = EntityState.Detached;
            if (await context.Users.AsNoTracking().AnyAsync(
                    existing => existing.NormalizedEmail == user.NormalizedEmail, cancellationToken))
            {
                return Result.Success();
            }

            throw;
        }
    }
}
