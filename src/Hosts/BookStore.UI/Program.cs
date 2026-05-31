var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<NotificationService>();

builder.Services.AddServiceDiscovery();

builder.Services.AddHttpClient<IBookStoreApiClient, BookStoreApiClient>(client => 
{
    client.BaseAddress = new Uri("https+http://bookstore-api");
})
.AddServiceDiscovery();

await builder.Build().RunAsync();
