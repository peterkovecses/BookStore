var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<NotificationService>();

builder.Services.AddHttpClient<IBookStoreApiClient, BookStoreApiClient>();

await builder.Build().RunAsync();
