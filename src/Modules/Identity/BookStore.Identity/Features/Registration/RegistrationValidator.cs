namespace BookStore.Identity.Features.Registration;

internal sealed class RegistrationValidator : AbstractValidator<RegisterCustomer>
{
    public RegistrationValidator(IOptions<IdentityOptions> options)
    {
        var password = options.Value.Password;
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        var passwordRule = RuleFor(x => x.Password).Cascade(CascadeMode.Stop)
            .NotEmpty().MinimumLength(password.RequiredLength);
        if (password.RequireDigit) passwordRule.Matches("[0-9]").WithMessage("Password must contain a digit.");
        if (password.RequireLowercase) passwordRule.Matches("[a-z]").WithMessage("Password must contain a lowercase letter.");
        if (password.RequireUppercase) passwordRule.Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.");
        if (password.RequireNonAlphanumeric) passwordRule.Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a non-alphanumeric character.");
        if (password.RequiredUniqueChars > 1)
            passwordRule.Must(value => value.Distinct().Count() >= password.RequiredUniqueChars)
                .WithMessage("Password does not contain enough distinct characters.");
    }
}
