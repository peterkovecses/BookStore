var builder = DistributedApplication.CreateBuilder(args);

var blobs = builder.AddAzureStorage("storage")
    .AddBlobs("book-covers");

var sql = builder.AddSqlServer("sqlserver");
var bookStoreDb = sql.AddDatabase("BookStoreDb");

builder.AddProject<Projects.BookStore_Api>("bookstore-api")
    .WithReference(blobs)
    .WithReference(bookStoreDb);

builder.Build().Run();
