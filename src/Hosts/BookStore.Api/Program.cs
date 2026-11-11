var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddApiServices(builder.Configuration);
builder.AddApiDataProtection();
builder.AddCatalogModuleServices();
builder.AddIdentityModuleServices();
builder.AddEmailDispatcherModuleServices();
builder.Services.AddRequests(Array.Empty<Assembly>())
    .AddOpenTelemetry()
    .AddLoggingBehavior()
    .AddCachingBehavior()
    .AddValidationBehavior();

var app = builder.Build();
app.VerifyApiDataProtection();
await app.InitialiseCatalogModuleAsync();
await app.InitialiseIdentityModuleAsync();
app.UseSerilogRequestLogging();
app.UseErrorHandling();
app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.MapCatalogEndpoints();
app.MapIdentityEndpoints();

app.Run();