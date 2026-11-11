namespace BookStore.Identity.Features.Verification;

internal sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailValidator()
    {
        RuleFor(request => request.UserId).NotEmpty().MaximumLength(256);
        RuleFor(request => request.EncodedToken).NotEmpty().MaximumLength(2048);
    }
}
