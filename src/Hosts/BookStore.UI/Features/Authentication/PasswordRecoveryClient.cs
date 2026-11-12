namespace BookStore.UI.Features.Authentication;

internal sealed class PasswordRecoveryClient(HttpClient client)
{
    internal async Task<PasswordRecoveryRequestStatus> RequestAsync(string email)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("api/identity/password/forgot", new { email });

            return response.IsSuccessStatusCode ? PasswordRecoveryRequestStatus.Success :
                response.StatusCode == HttpStatusCode.BadRequest ? PasswordRecoveryRequestStatus.Invalid : PasswordRecoveryRequestStatus.Unavailable;
        }
        catch (HttpRequestException)
        {
            return PasswordRecoveryRequestStatus.Unavailable;
        }
        catch (OperationCanceledException)
        {
            return PasswordRecoveryRequestStatus.Unavailable;
        }
    }

    internal async Task<PasswordResetStatus> ResetAsync(IdentityLink? link, string newPassword)
    {
        if (link is null || !IdentityLinkValidator.IsValid(link) ||
            string.IsNullOrWhiteSpace(newPassword) || newPassword.Length > 1024)
        {
            return PasswordResetStatus.Invalid;
        }

        try
        {
            using var response = await client.PostAsJsonAsync("api/identity/password/reset",
                new { link.UserId, link.Token, newPassword });

            return response.IsSuccessStatusCode ? PasswordResetStatus.Success :
                response.StatusCode == HttpStatusCode.BadRequest ? PasswordResetStatus.Invalid : PasswordResetStatus.Unavailable;
        }
        catch (HttpRequestException)
        {
            return PasswordResetStatus.Unavailable;
        }
        catch (OperationCanceledException)
        {
            return PasswordResetStatus.Unavailable;
        }
    }
}

internal enum PasswordRecoveryRequestStatus
{
    Success,
    Invalid,
    Unavailable
}

internal enum PasswordResetStatus
{
    Success,
    Invalid,
    Unavailable
}
