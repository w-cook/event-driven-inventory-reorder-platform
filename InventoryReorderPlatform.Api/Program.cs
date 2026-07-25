using Azure.Messaging.ServiceBus;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Contracts.Configuration;
using InventoryReorderPlatform.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<AppDbContext>(
    connectionName: "inventorydb");

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            DemoAuthenticationDefaults.Scheme;

        options.DefaultChallengeScheme =
            DemoAuthenticationDefaults.Scheme;
    })
    .AddScheme<
        AuthenticationSchemeOptions,
        DemoAuthenticationHandler>(
        DemoAuthenticationDefaults.Scheme,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AppPolicies.InventoryRead,
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(
                AppRoles.Viewer,
                AppRoles.Operator,
                AppRoles.Administrator);
        });

    options.AddPolicy(
        AppPolicies.InventoryOperate,
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(
                AppRoles.Operator,
                AppRoles.Administrator);
        });

    options.AddPolicy(
        AppPolicies.AdminOnly,
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(
                AppRoles.Administrator);
        });
});

builder.Services.Configure<ServiceBusOptions>(
    builder.Configuration.GetSection("ServiceBus"));

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<ServiceBusOptions>>().Value;

    return new ServiceBusClient(options.ConnectionString);
});

builder.Services.AddSingleton<ReorderMessagePublisher>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    dbContext.Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();