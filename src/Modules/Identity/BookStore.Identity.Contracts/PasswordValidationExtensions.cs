namespace BookStore.Identity.Contracts;

public static class PasswordValidationExtensions
{
    public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(
        this IRuleBuilderInitial<T, string> rule, PasswordPolicy policy)
    {
        var password = rule.Cascade(CascadeMode.Stop).NotEmpty().MinimumLength(policy.RequiredLength);
        if (policy.RequireDigit) password.Matches("[0-9]").WithMessage("Password must contain a digit.");
        if (policy.RequireLowercase) password.Matches("[a-z]").WithMessage("Password must contain a lowercase letter.");
        if (policy.RequireUppercase) password.Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.");
        if (policy.RequireNonAlphanumeric) password.Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a non-alphanumeric character.");
        if (policy.RequiredUniqueChars > 1)
            password.Must(value => value.Distinct().Count() >= policy.RequiredUniqueChars)
                .WithMessage($"Password must contain at least {policy.RequiredUniqueChars} distinct characters.");

        return password;
    }
}
