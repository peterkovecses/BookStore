var builder = DistributedApplication.CreateBuilder(args);

var blobs = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite => 
    {
        azurite.WithDataVolume();
        azurite.WithLifetime(ContainerLifetime.Session);
    })
    .AddBlobs("book-covers");

var sql = builder.AddSqlServer("sqlserver")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Session);

var bookStoreDb = sql.AddDatabase("BookStoreDb");

builder.AddProject<Projects.BookStore_Api>("bookstore-api")
    .WithReference(blobs)
    .WithReference(bookStoreDb)
    .WaitFor(blobs)
    .WaitFor(bookStoreDb);

builder.Build().Run();
