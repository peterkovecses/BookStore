var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddServiceDiscovery();

builder.Services.AddHttpClient("bookstore-api", client => 
{
    client.BaseAddress = new("https://bookstore-api");
});

builder.Services.AddScoped<IBookStoreApiClient, BookStoreApiClient>();

await builder.Build().RunAsync();
