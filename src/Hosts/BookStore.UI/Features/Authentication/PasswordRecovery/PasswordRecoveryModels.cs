namespace BookStore.UI.Features.Authentication;

internal sealed class ForgotPasswordModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

internal sealed class PasswordResetModel
{
    [Required, MinLength(6, ErrorMessage = "Use at least 6 characters."), MaxLength(1024)]
    [RegularExpression("^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).+$",
        ErrorMessage = "Include an uppercase letter, a lowercase letter, a number, and a symbol.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
