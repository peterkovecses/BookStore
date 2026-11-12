namespace BookStore.Identity.Features.Registration;

internal sealed class RegistrationHandler(
    UserManager<ApplicationUser> users,
    IIdentityDbContext context,
    IUserIdGenerator idGenerator,
    IVerificationRequestFactory verificationRequests)
    : IRequestHandler<RegisterCommand, Result>
{
    public async Task<Result> HandleAsync(RegisterCommand request, CancellationToken cancellationToken)
    {
        var userId = idGenerator.Create();

        try
        {
            var attempt = await context.ExecuteInTransactionAsync(async (transaction, token) =>
            {
                var user = new ApplicationUser
                {
                    Id = userId,
                    Email = request.Email,
                    UserName = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName
                };
                var result = await users.CreateAsync(user, request.Password);
                if (result.Succeeded)
                {
                    var message = await verificationRequests.CreateAsync(user, token);
                    context.OutboxMessages.Add(message);
                    await context.SaveChangesAsync(token);
                    await transaction.CommitAsync(token);

                    return new RegistrationAttempt(null);
                }

                await transaction.RollbackAsync(token);

                return new RegistrationAttempt(result);
            }, cancellationToken);
            if (attempt.Result is null)
            {
                return Result.Success();
            }

            var errors = attempt.Result.Errors.ToList();
            var validationErrors = errors.Where(error => !IsDuplicateError(error)).ToList();
            if (errors.Count > 0 && validationErrors.Count == 0)
            {
                if (await EmailExistsAsync(users.NormalizeEmail(request.Email), cancellationToken))
                {
                    return Result.Success();
                }

                throw new InvalidOperationException("Registration could not be completed due to a conflicting account.");
            }

            return ToValidationResult(validationErrors);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            context.ClearChangeTracker();
            var normalizedEmail = users.NormalizeEmail(request.Email);
            var normalizedUserName = users.NormalizeName(request.Email);
            var matchingUser = await context.Users.AsNoTracking()
                .Where(existing => existing.NormalizedEmail == normalizedEmail)
                .Select(existing => new { existing.Id })
                .SingleOrDefaultAsync(cancellationToken);
            // Check the other unique keys in the current user model before treating this as an email duplicate.
            if (matchingUser is not null &&
                !await context.Users.AsNoTracking().AnyAsync(existing =>
                    existing.Id == userId ||
                    (existing.NormalizedUserName == normalizedUserName && existing.Id != matchingUser.Id),
                    cancellationToken))
            {
                return Result.Success();
            }

            throw;
        }
    }

    private static bool IsDuplicateError(IdentityError error) =>
        error.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName);

    private static Result ToValidationResult(IEnumerable<IdentityError> errors)
    {
        var metadata = new Dictionary<string, object>
        {
            ["Registration"] = errors.Where(error => !IsDuplicateError(error)).Select(error => error.Description).ToList()
        };

        return Result.Failure(Error.Validation("Validation", "Validation failed.", metadata));
    }

    private Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.AsNoTracking().AnyAsync(existing => existing.NormalizedEmail == normalizedEmail, cancellationToken);

    private sealed record RegistrationAttempt(IdentityResult? Result);
}
