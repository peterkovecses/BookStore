var builder = DistributedApplication.CreateBuilder(args);

var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite => 
    {
        azurite.WithLifetime(ContainerLifetime.Session)
            .WithDataVolume("bookstore-local-azurite-data");
    })
    .AddBlobs("book-covers");

var sql = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Session)
    .WithDataVolume("bookstore-local-sqlserver-data");

var bookStoreDb = sql.AddDatabase("BookStoreDb");

var bookStoreApi = builder.AddProject<Projects.BookStore_Api>("bookstore-api")
    .WithExternalHttpEndpoints()
    .WithHttpsEndpoint(port: 5001)
    .WithReference(blobs)
    .WithReference(bookStoreDb)
    .WaitFor(blobs)
    .WaitFor(bookStoreDb);

if (builder.Environment.IsDevelopment())
{
    var mailpit = builder.AddMailPit("mailpit")
        .WithDataVolume("bookstore-local-mailpit-data");
    bookStoreApi
        .WithEnvironment("EmailDispatcher__Smtp__Host", mailpit.GetEndpoint("smtp").Property(EndpointProperty.Host))
        .WithEnvironment("EmailDispatcher__Smtp__Port", mailpit.GetEndpoint("smtp").Property(EndpointProperty.Port))
        .WithEnvironment("EmailDispatcher__Smtp__Security", "None")
        .WaitFor(mailpit);
}

var bookStoreUi = builder.AddProject<Projects.BookStore_UI>("bookstore-ui")
    .WithReference(bookStoreApi)
    .WaitFor(bookStoreApi);

bookStoreApi.WithEnvironment("AllowedOrigins", bookStoreUi.GetEndpoint("https"));
if (builder.Environment.IsDevelopment())
{
    bookStoreApi.WithEnvironment("Identity__Verification__LinkBaseUrl",
        ReferenceExpression.Create($"{bookStoreUi.GetEndpoint("https")}/verify-email"));
    bookStoreApi.WithEnvironment("Identity__PasswordRecovery__LinkBaseUrl",
        ReferenceExpression.Create($"{bookStoreUi.GetEndpoint("https")}/reset-password"));
}

builder.Build().Run();
