namespace BookStore.UI.Features.Authentication;

internal sealed class LoginModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(1024)]
    public string Password { get; set; } = string.Empty;
}
