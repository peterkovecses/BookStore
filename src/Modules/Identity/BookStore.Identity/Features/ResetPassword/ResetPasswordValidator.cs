namespace BookStore.Identity.Features.ResetPassword;

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(request => request.UserId).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(256);
        RuleFor(request => request.Token).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(2048);
        RuleFor(request => request.NewPassword).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(1024);
    }
}
