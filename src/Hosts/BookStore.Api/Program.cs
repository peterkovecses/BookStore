var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddApiServices();
builder.AddCatalogModuleServices();

var app = builder.Build();
await app.InitialiseCatalogModuleAsync();
app.UseSerilogRequestLogging();
app.UseErrorHandling();
app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();
app.MapCatalogEndpoints();

app.Run();