var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.InventoryReorderPlatform_Api>("inventoryreorderplatform-api");

builder.AddProject<Projects.InventoryReorderPlatform_Processor>("inventoryreorderplatform-processor");

builder.Build().Run();
