using InventoryReorderPlatform.SupplierMockApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<SupplierDbContext>(
    connectionName: "supplierdb");

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext =
        scope.ServiceProvider
            .GetRequiredService<SupplierDbContext>();

    if (dbContext.Database.IsSqlServer())
    {
        dbContext.Database.Migrate();
    }
    else
    {
        dbContext.Database.EnsureCreated();
    }
}

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();

public partial class Program
{
}