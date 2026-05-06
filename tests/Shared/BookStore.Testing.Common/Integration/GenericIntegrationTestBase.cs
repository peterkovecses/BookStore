namespace BookStore.Testing.Common.Integration;

/// <summary>
/// Generic base class for integration tests using a factory with database reset capability.
/// </summary>
/// <typeparam name="TFactory">The WebApplicationFactory type that implements IAsyncLifetime</typeparam>
public abstract class GenericIntegrationTestBase<TFactory> : IAsyncLifetime
    where TFactory : class, IAsyncLifetime
{
    private readonly Random _random = new();

    /// <summary>
    /// The HTTP client to use for API calls.
    /// </summary>
    protected readonly HttpClient Client;

    /// <summary>
    /// The factory instance used to create the HTTP client.
    /// </summary>
    protected readonly TFactory Factory;

    static GenericIntegrationTestBase()
    {
        // Force English culture to ensure consistent validation messages across different environments
        var culture = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    protected GenericIntegrationTestBase(TFactory factory)
    {
        Factory = factory;
        
        // Try to get CreateClient method - this assumes TFactory has a CreateClient method
        var createClientMethod = factory.GetType().GetMethod("CreateClient", Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"Factory type '{factory.GetType().Name}' must have a CreateClient() method.");
        
        Client = (HttpClient)createClientMethod.Invoke(factory, [])!;
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
        // Try to call ResetDatabaseAsync if the factory implements it
        var resetMethod = Factory.GetType().GetMethod(
            "ResetDatabaseAsync",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        var task = (Task?)resetMethod?.Invoke(Factory, []);
        if (task is not null)
        {
            await task;
        }

        GC.SuppressFinalize(this);
    }
}