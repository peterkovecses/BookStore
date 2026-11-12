namespace BookStore.UI.Features.Authentication;

internal sealed class RegistrationPasswordValidator : AbstractValidator<RegistrationModel>
{
    internal RegistrationPasswordValidator(PasswordPolicy policy) =>
        RuleFor(model => model.Password).ApplyPasswordRules(policy);
}
