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
    .WithReference(blobs)
    .WithReference(bookStoreDb)
    .WaitFor(blobs)
    .WaitFor(bookStoreDb);

var bookStoreUi = builder.AddProject<Projects.BookStore_UI>("bookstore-ui")
    .WithReference(bookStoreApi)
    .WaitFor(bookStoreApi);

bookStoreApi.WithReference(bookStoreUi);

builder.Build().Run();
