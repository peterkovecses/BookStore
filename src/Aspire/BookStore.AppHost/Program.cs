var builder = DistributedApplication.CreateBuilder(args);

var blobs = builder.AddAzureStorage("storage")
    .AddBlobs("book-covers");

builder.AddProject<Projects.BookStore_Api>("bookstore-api")
    .WithReference(blobs);

builder.Build().Run();
