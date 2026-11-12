namespace BookStore.UI.Features.Authentication;

internal sealed class RegistrationClient(HttpClient client)
{
    internal async Task<RegistrationResult> RegisterAsync(RegistrationModel model)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("api/identity/registration", model);
            if (response.IsSuccessStatusCode)
            {
                return new(RegistrationStatus.Success);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
                if (problem?.Errors is { Count: > 0 })
                {
                    return new(RegistrationStatus.Invalid, problem.Errors);
                }
            }

            return new(RegistrationStatus.Unavailable);
        }
        catch (HttpRequestException)
        {
            return new(RegistrationStatus.Unavailable);
        }
        catch (OperationCanceledException)
        {
            return new(RegistrationStatus.Unavailable);
        }
        catch (JsonException)
        {
            return new(RegistrationStatus.Unavailable);
        }
    }

    internal async Task<VerificationStatus> ConfirmAsync(VerificationLink? link)
    {
        if (link is null || string.IsNullOrWhiteSpace(link.UserId) || link.UserId.Length > 256 ||
            string.IsNullOrWhiteSpace(link.Token) || link.Token.Length > 2048 ||
            !link.Token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            return VerificationStatus.Invalid;
        }

        return await SendAsync("api/identity/verification/confirmation", link);
    }

    internal Task<VerificationStatus> ResendAsync(string email) =>
        SendAsync("api/identity/verification/resend", new { email });

    private async Task<VerificationStatus> SendAsync<T>(string path, T request)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(path, request);

            return response.IsSuccessStatusCode ? VerificationStatus.Success :
                response.StatusCode == HttpStatusCode.BadRequest ? VerificationStatus.Invalid : VerificationStatus.Unavailable;
        }
        catch (HttpRequestException)
        {
            return VerificationStatus.Unavailable;
        }
        catch (OperationCanceledException)
        {
            return VerificationStatus.Unavailable;
        }
    }
}

internal sealed record RegistrationResult(RegistrationStatus Status, IDictionary<string, string[]>? Errors = null);

internal enum RegistrationStatus { Success, Invalid, Unavailable }

internal sealed record VerificationLink(string UserId, string Token);

internal enum VerificationStatus { Success, Invalid, Unavailable }
