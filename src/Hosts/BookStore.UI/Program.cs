var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<NotificationService>();

builder.Services.AddServiceDiscovery();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<BrowserSessionLock>();
builder.Services.AddScoped<ISessionLock>(services => services.GetRequiredService<BrowserSessionLock>());
builder.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<AuthenticationSession>());
builder.Services.AddHttpClient("identity", client =>
{
    client.BaseAddress = new Uri("https+http://bookstore-api");
}).AddServiceDiscovery();
builder.Services.AddScoped(services => new AuthenticationSession(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("identity"),
    services.GetRequiredService<ISessionLock>()));
builder.Services.AddScoped(services => new RegistrationClient(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("identity")));
builder.Services.AddScoped(services => new PasswordPolicyClient(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("identity"),
    services.GetRequiredService<ILogger<PasswordPolicyClient>>()));
builder.Services.AddScoped(services => new PasswordRecoveryClient(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("identity")));

builder.Services.AddHttpClient<IBookStoreApiClient, BookStoreApiClient>(client => 
{
    client.BaseAddress = new Uri("https+http://bookstore-api");
})
.AddServiceDiscovery();

await builder.Build().RunAsync();
