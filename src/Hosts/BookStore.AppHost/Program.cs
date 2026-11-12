var builder = DistributedApplication.CreateBuilder(args);

var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite => 
    {
        azurite.WithLifetime(ContainerLifetime.Session);
    })
    .AddBlobs("book-covers");

var sql = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Session);

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
    var mailpit = builder.AddMailPit("mailpit");
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
}

builder.Build().Run();
