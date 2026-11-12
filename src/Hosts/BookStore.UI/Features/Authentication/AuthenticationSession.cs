namespace BookStore.UI.Features.Authentication;

internal sealed class AuthenticationSession(HttpClient client, ISessionLock sessionLock) : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AuthenticationState _state = Anonymous;
    private string? _accessToken;
    private bool _restored;

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(_state);

    internal async Task RestoreAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!_restored)
            {
                _restored = true;
                await RefreshCoreAsync();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<LoginResult> LoginAsync(string email, string password)
    {
        await _gate.WaitAsync();
        try
        {
            _restored = true;
            using var response = await SendSessionRequestAsync("login", JsonContent.Create(new { email, password }));
            if (response.IsSuccessStatusCode)
            {
                return await SetTokenAsync(response) ? LoginResult.Success : LoginResult.Unavailable;
            }

            Clear();
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

                return problem?.Title == "Email address has not been confirmed."
                    ? LoginResult.Unconfirmed : LoginResult.Invalid;
            }

            return LoginResult.Unavailable;
        }
        catch (HttpRequestException)
        {
            Clear();
            return LoginResult.Unavailable;
        }
        catch (OperationCanceledException)
        {
            Clear();
            return LoginResult.Unavailable;
        }
        catch (JSException)
        {
            Clear();
            return LoginResult.Unavailable;
        }
        catch (JsonException)
        {
            Clear();
            return LoginResult.Unavailable;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<bool> LogoutAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _restored = true;
            using var response = await SendSessionRequestAsync("logout");

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (JSException)
        {
            return false;
        }
        finally
        {
            Clear();
            _gate.Release();
        }
    }

    // The factory must create a new relative API request on each invocation.
    internal async Task<HttpResponseMessage> SendAuthenticatedAsync(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken = default)
    {
        await RestoreAsync();
        var token = _accessToken;
        var (response, method) = await SendApiRequestAsync(requestFactory, token, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || token is null)
        {
            return response;
        }

        try
        {
            await _gate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            response.Dispose();
            throw;
        }
        try
        {
            if (_accessToken == token)
            {
                await RefreshCoreAsync();
            }
            token = _accessToken;
        }
        finally
        {
            _gate.Release();
        }

        if (token is null || (method != HttpMethod.Get && method != HttpMethod.Head))
        {
            return response;
        }

        response.Dispose();
        var (retry, _) = await SendApiRequestAsync(requestFactory, token, cancellationToken, method);

        return retry;
    }

    private async Task<(HttpResponseMessage Response, HttpMethod Method)> SendApiRequestAsync(
        Func<HttpRequestMessage> requestFactory, string? token, CancellationToken cancellationToken, HttpMethod? expectedMethod = null)
    {
        using var request = requestFactory();
        if (request.RequestUri is null || request.RequestUri.IsAbsoluteUri ||
            !request.RequestUri.OriginalString.StartsWith("api/", StringComparison.Ordinal) ||
            request.RequestUri.OriginalString.Contains('\\'))
        {
            throw new InvalidOperationException("Authenticated requests must use a relative API URI.");
        }

        if (expectedMethod is not null && request.Method != expectedMethod)
        {
            throw new InvalidOperationException("A retried request must use the original HTTP method.");
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return (await client.SendAsync(request, cancellationToken), request.Method);
    }

    private async Task RefreshCoreAsync()
    {
        try
        {
            using var response = await SendSessionRequestAsync("refresh");
            if (!response.IsSuccessStatusCode || !await SetTokenAsync(response))
            {
                Clear();
            }
        }
        catch (HttpRequestException)
        {
            Clear();
        }
        catch (OperationCanceledException)
        {
            Clear();
        }
        catch (JSException)
        {
            Clear();
        }
        catch (JsonException)
        {
            Clear();
        }
    }

    private async Task<HttpResponseMessage> SendSessionRequestAsync(string endpoint, HttpContent? content = null)
    {
        await sessionLock.AcquireAsync();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/identity/{endpoint}") { Content = content };
            request.Headers.Add("X-BookStore-Request", "1");
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

            return await client.SendAsync(request);
        }
        finally
        {
            await sessionLock.ReleaseAsync();
        }
    }

    private async Task<bool> SetTokenAsync(HttpResponseMessage response)
    {
        var token = await response.Content.ReadFromJsonAsync<AccessToken>();
        if (string.IsNullOrWhiteSpace(token?.AccessTokenValue))
        {
            Clear();
            return false;
        }

        _accessToken = token.AccessTokenValue;
        _state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "BookStore")));
        NotifyAuthenticationStateChanged(Task.FromResult(_state));
        return true;
    }

    private void Clear()
    {
        _accessToken = null;
        _state = Anonymous;
        NotifyAuthenticationStateChanged(Task.FromResult(_state));
    }

    private sealed record AccessToken([property: System.Text.Json.Serialization.JsonPropertyName("accessToken")] string AccessTokenValue);
}

internal enum LoginResult
{
    Success,
    Invalid,
    Unconfirmed,
    Unavailable
}
