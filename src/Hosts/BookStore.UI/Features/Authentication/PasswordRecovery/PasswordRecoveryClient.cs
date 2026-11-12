namespace BookStore.UI.Features.Authentication;

internal sealed class PasswordRecoveryClient(HttpClient client)
{
    internal async Task<PasswordRecoveryRequestResult> RequestAsync(string email)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("api/identity/password/forgot", new { email });

            var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds;
            var retryAfter = seconds is > 0 and <= 3600 ? (int)Math.Ceiling(seconds.Value) : (int?)null;

            return response.StatusCode switch
            {
                HttpStatusCode.NoContent => new(PasswordRecoveryRequestStatus.Success, retryAfter ?? 60),
                HttpStatusCode.BadRequest => new(PasswordRecoveryRequestStatus.Invalid, null),
                HttpStatusCode.TooManyRequests => new(PasswordRecoveryRequestStatus.RateLimited, retryAfter),
                _ => new(PasswordRecoveryRequestStatus.Unavailable, null)
            };
        }
        catch (HttpRequestException)
        {
            return new(PasswordRecoveryRequestStatus.Unavailable, null);
        }
        catch (OperationCanceledException)
        {
            return new(PasswordRecoveryRequestStatus.Unavailable, null);
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
    RateLimited,
    Unavailable
}

internal sealed record PasswordRecoveryRequestResult(PasswordRecoveryRequestStatus Status, int? RetryAfterSeconds);

internal enum PasswordResetStatus
{
    Success,
    Invalid,
    Unavailable
}
