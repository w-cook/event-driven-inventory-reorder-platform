var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
                 .WithLifetime(ContainerLifetime.Persistent);

var inventorydb = sql.AddDatabase("inventorydb");

builder.AddProject<Projects.InventoryReorderPlatform_Api>("api")
       .WithReference(inventorydb)
       .WaitFor(inventorydb);

builder.AddProject<Projects.InventoryReorderPlatform_Processor>("processor")
       .WithReference(inventorydb)
       .WaitFor(inventorydb);

builder.Build().Run();
