using InventoryReorderPlatform.SupplierMockApi.Behavior;
using InventoryReorderPlatform.SupplierMockApi.Data;
using InventoryReorderPlatform.SupplierMockApi.OpenApi;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<SupplierDbContext>(
    connectionName: "supplierdb");

builder.Services
    .AddOptions<SupplierBehaviorOptions>()
    .Bind(
        builder.Configuration.GetSection(
            SupplierBehaviorOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => Enum.IsDefined(
            typeof(SupplierBehaviorMode),
            options.Mode),
        "Supplier behavior mode is invalid.")
    .ValidateOnStart();

builder.Services.AddSingleton<
    ISupplierBehaviorSimulator,
    SupplierBehaviorSimulator>();

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer<
        SupplierSchemaExamplesTransformer>();
});

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