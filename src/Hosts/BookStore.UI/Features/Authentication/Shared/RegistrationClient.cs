namespace BookStore.UI.Features.Authentication;

internal sealed class RegistrationClient(HttpClient client)
{
    internal async Task<RegistrationResult> RegisterAsync(RegistrationModel model)
    {
        try
        {
            var request = new RegistrationRequest
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                Password = model.Password
            };
            using var response = await client.PostAsJsonAsync("api/identity/registration", request);
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

    internal async Task<VerificationStatus> ConfirmAsync(IdentityLink? link)
    {
        if (!IdentityLinkValidator.IsValid(link))
        {
            return VerificationStatus.Invalid;
        }

        return await SendAsync("api/identity/verification/confirmation", link);
    }

    internal async Task<ResendResult> ResendAsync(string email)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("api/identity/verification/resend", new { email });
            var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds;
            var retryAfter = seconds is > 0 and <= 3600 ? (int)Math.Ceiling(seconds.Value) : (int?)null;

            return response.StatusCode switch
            {
                HttpStatusCode.NoContent => new(VerificationStatus.Success, retryAfter ?? 60),
                HttpStatusCode.BadRequest => new(VerificationStatus.Invalid, null),
                HttpStatusCode.TooManyRequests => new(VerificationStatus.RateLimited, retryAfter),
                _ => new(VerificationStatus.Unavailable, null)
            };
        }
        catch (HttpRequestException)
        {
            return new(VerificationStatus.Unavailable, null);
        }
        catch (OperationCanceledException)
        {
            return new(VerificationStatus.Unavailable, null);
        }
    }

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

internal sealed record ResendResult(VerificationStatus Status, int? RetryAfterSeconds);

internal enum RegistrationStatus { Success, Invalid, Unavailable }

internal enum VerificationStatus { Success, Invalid, Unavailable, RateLimited }
