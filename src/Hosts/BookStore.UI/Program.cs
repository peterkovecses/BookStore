var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddServiceDiscovery();

builder.Services.AddScoped<NotificationService>();

builder.Services.AddHttpClient<IBookStoreApiClient, BookStoreApiClient>(client => 
{
    client.BaseAddress = new("https://bookstore-api");
});

await builder.Build().RunAsync();
