var builder = DistributedApplication.CreateBuilder(args);

var sql = builder
    .AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent);

var inventorydb = sql.AddDatabase("inventorydb");
var supplierdb = sql.AddDatabase("supplierdb");

var api = builder
    .AddProject<Projects.InventoryReorderPlatform_Api>("api")
    .WithReference(inventorydb)
    .WaitFor(inventorydb);

builder
    .AddProject<Projects.InventoryReorderPlatform_SupplierMockApi>(
        "supplier")
    .WithReference(supplierdb)
    .WaitFor(supplierdb);

builder
    .AddProject<Projects.InventoryReorderPlatform_Processor>(
        "processor")
    .WithReference(inventorydb)
    .WaitFor(inventorydb);

builder
    .AddViteApp("client", "../client")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();