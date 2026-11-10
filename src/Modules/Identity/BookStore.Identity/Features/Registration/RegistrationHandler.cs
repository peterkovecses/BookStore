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
            if (result.Succeeded)
            {
                return Result.Success();
            }

            var errors = result.Errors.ToList();
            var otherErrors = errors.Where(error => !IsDuplicateError(error)).ToList();
            if (errors.Count > 0 && otherErrors.Count == 0)
            {
                if (await EmailExistsAsync(users.NormalizeEmail(request.Email), cancellationToken))
                {
                    return Result.Success();
                }

                throw new InvalidOperationException("Registration could not be completed due to a conflicting account.");
            }

            var metadata = new Dictionary<string, object>
            {
                ["Registration"] = otherErrors.Select(error => error.Description).ToList()
            };

            return Result.Failure(Error.Validation("Validation", "Validation failed.", metadata));
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            context.Entry(user).State = EntityState.Detached;
            var matchingUser = await context.Users.AsNoTracking()
                .Where(existing => existing.NormalizedEmail == user.NormalizedEmail)
                .Select(existing => new { existing.Id })
                .SingleOrDefaultAsync(cancellationToken);
            // Check the other unique keys in the current user model before treating this as an email duplicate.
            if (matchingUser is not null &&
                !await context.Users.AsNoTracking().AnyAsync(existing =>
                    existing.Id == user.Id ||
                    (existing.NormalizedUserName == user.NormalizedUserName && existing.Id != matchingUser.Id),
                    cancellationToken))
            {
                return Result.Success();
            }

            throw;
        }
    }

    private static bool IsDuplicateError(IdentityError error) =>
        error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName);

    private Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().AnyAsync(existing => existing.NormalizedEmail == normalizedEmail, cancellationToken);
}
