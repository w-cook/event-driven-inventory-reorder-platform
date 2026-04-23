using Azure.Messaging.ServiceBus;
using InventoryReorderPlatform.Contracts.Configuration;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Processor;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<AppDbContext>(connectionName: "inventorydb");

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
