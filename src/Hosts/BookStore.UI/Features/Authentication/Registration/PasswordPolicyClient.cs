namespace BookStore.UI.Features.Authentication;

internal sealed class PasswordPolicyClient(HttpClient client, ILogger<PasswordPolicyClient> logger)
{
    internal async Task<PasswordPolicy?> GetAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/identity/password-policy");
            request.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var policy = await response.Content.ReadFromJsonAsync<PasswordPolicy>();
                if (policy is not null && policy.IsValid())
                {
                    return policy;
                }
            }
        }
        catch (HttpRequestException)
        {
        }
        catch (OperationCanceledException)
        {
        }
        catch (JsonException)
        {
        }
        catch (NotSupportedException)
        {
        }

        logger.PasswordPolicyUnavailable();

        return null;
    }
}

internal static partial class PasswordPolicyClientLogging
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Password policy could not be retrieved or was invalid; server validation will be used.")]
    internal static partial void PasswordPolicyUnavailable(this ILogger<PasswordPolicyClient> logger);
}
