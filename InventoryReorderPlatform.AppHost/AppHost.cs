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

var supplier = builder
    .AddProject<
        Projects.InventoryReorderPlatform_SupplierMockApi>(
        "supplier")
    .WithReference(supplierdb)
    .WaitFor(supplierdb);

builder
    .AddProject<
        Projects.InventoryReorderPlatform_Processor>(
        "processor")
    .WithReference(inventorydb)
    .WithReference(supplier)
    .WaitFor(inventorydb)
    .WaitFor(supplier);

builder
    .AddViteApp("client", "../client")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();