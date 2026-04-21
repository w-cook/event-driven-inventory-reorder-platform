using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Processor;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
