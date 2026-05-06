namespace BookStore.Testing.Common.Integration;

/// <summary>
/// Generic base class for integration tests using a factory with database reset capability.
/// </summary>
/// <typeparam name="TMarker">The marker type for the WebApplicationFactory</typeparam>
/// <typeparam name="TFactory">The WebApplicationFactory type that implements IAsyncLifetime and IDatabaseResettable</typeparam>
public abstract class IntegrationTestBase<TMarker, TFactory>(TFactory factory) : IAsyncLifetime
    where TMarker : class
    where TFactory : WebApplicationFactory<TMarker>, IAsyncLifetime
{
    private readonly Random _random = new();

    /// <summary>
    /// The HTTP client to use for API calls.
    /// </summary>
    protected readonly HttpClient Client = factory.CreateClient();

    /// <summary>
    /// The factory instance used to create the HTTP client.
    /// </summary>
    protected readonly TFactory Factory = factory;

    static IntegrationTestBase()
    {
        // Force English culture to ensure consistent validation messages across different environments
        var culture = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    /// <summary>
    /// Set the authorization header with a JWT token.
    /// </summary>
    /// <param name="userId">The user ID to use for the token. If null, a random ID is generated.</param>
    protected void SetAuthHeader(int? userId = null)
    {
        userId ??= _random.Next(1, 1000);
        var authToken = JwtHelpers.GenerateToken(userId.Value);
        Client.DefaultRequestHeaders.SetAuthorizationHeader(authToken);
    }

    /// <summary>
    /// Initialize the test (called before each test).
    /// </summary>
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Cleanup after the test (reset database).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Factory is IDatabaseResettable resettable)
        {
            await resettable.ResetDatabaseAsync();
        }

        GC.SuppressFinalize(this);
    }
}