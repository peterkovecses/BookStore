namespace BookStore.Identity.Features.Registration;

internal sealed class RegistrationValidator : AbstractValidator<RegisterCommand>
{
    public RegistrationValidator(IOptions<IdentityOptions> options)
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).ApplyPasswordRules(PasswordPolicyFactory.Create(options.Value.Password));
    }
}
