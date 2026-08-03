using Azure.Messaging.ServiceBus;
using InventoryReorderPlatform.Contracts.Configuration;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Processor;
using InventoryReorderPlatform.Processor.Processing;
using InventoryReorderPlatform.Processor.Supplier;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<AppDbContext>(connectionName: "inventorydb");

var supplierBaseUrl =
    builder.Configuration["Supplier:BaseUrl"];

if (string.IsNullOrWhiteSpace(supplierBaseUrl))
{
    throw new InvalidOperationException(
        "Supplier:BaseUrl must be configured.");
}

if (!Uri.TryCreate(
        supplierBaseUrl,
        UriKind.Absolute,
        out var supplierBaseUri))
{
    throw new InvalidOperationException(
        "Supplier:BaseUrl must be a valid absolute URI.");
}

builder.Services.AddHttpClient<
    ISupplierOrderClient,
    SupplierOrderClient>(client =>
    {
        client.BaseAddress = supplierBaseUri;
        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddScoped<
    IReorderMessageProcessor,
    ReorderMessageProcessor>();

builder.Services.AddHostedService<Worker>();

builder.Services.Configure<ServiceBusOptions>(
    builder.Configuration.GetSection("ServiceBus"));

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<ServiceBusOptions>>().Value;

    return new ServiceBusClient(options.ConnectionString);
});

var host = builder.Build();
host.Run();
