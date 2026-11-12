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
            var attempt = await context.ExecuteInTransactionAsync(
                (transaction, token) => RegisterAsync(request, userId, transaction, token), cancellationToken);
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
            if (await IsEmailConflictAsync(request.Email, userId, cancellationToken))
            {
                return Result.Success();
            }

            throw;
        }
    }

    private async Task<RegistrationAttempt> RegisterAsync(
        RegisterCommand request, string userId, IIdentityDbTransaction transaction, CancellationToken cancellationToken)
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
            var message = await verificationRequests.CreateAsync(user, cancellationToken);
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new RegistrationAttempt(null);
        }

        await transaction.RollbackAsync(cancellationToken);

        return new RegistrationAttempt(result);
    }

    private async Task<bool> IsEmailConflictAsync(string email, string userId, CancellationToken cancellationToken)
    {
        var normalizedEmail = users.NormalizeEmail(email);
        var normalizedUserName = users.NormalizeName(email);
        var matchingUser = await context.Users.AsNoTracking()
            .Where(existing => existing.NormalizedEmail == normalizedEmail)
            .Select(existing => new { existing.Id })
            .SingleOrDefaultAsync(cancellationToken);

        return matchingUser is not null &&
            !await context.Users.AsNoTracking().AnyAsync(existing =>
                existing.Id == userId ||
                (existing.NormalizedUserName == normalizedUserName && existing.Id != matchingUser.Id),
                cancellationToken);
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
