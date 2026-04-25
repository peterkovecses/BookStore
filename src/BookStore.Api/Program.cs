var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddApiServices();
builder.AddCatalogModuleServices();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseErrorHandling();
app.MapDefaultEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();

app.Run();