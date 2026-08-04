using System.Security.Claims;
using System.Text;
using Azure.Messaging.ServiceBus;
using InventoryReorderPlatform.Api.Middleware;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Contracts.Configuration;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddSqlServerDbContext<AppDbContext>(
    connectionName: "inventorydb");

// Add services to the container.
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(
        builder.Configuration.GetSection(
            JwtOptions.SectionName))
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.Issuer),
        "JWT issuer is required.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.Audience),
        "JWT audience is required.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.SigningKey),
        "JWT signing key is required.")
    .Validate(
        options =>
            Encoding.UTF8.GetByteCount(
                options.SigningKey) >= 32,
        "JWT signing key must be at least 32 bytes.")
    .Validate(
        options =>
            options.AccessTokenMinutes > 0,
        "JWT access-token duration must be positive.")
    .ValidateOnStart();

builder.Services.AddScoped<
    IJwtTokenService,
    JwtTokenService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<
    ICorrelationIdAccessor,
    CorrelationIdAccessor>();

builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.AddScoped<IdentityBootstrapper>();

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "JWT configuration is missing.");

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(
        JwtBearerDefaults.AuthenticationScheme,
        options =>
        {
            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                jwtOptions.SigningKey)),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userId =
                        context.Principal?.FindFirstValue(
                            ClaimTypes.NameIdentifier);

                    var tokenSecurityStamp =
                        context.Principal?.FindFirstValue(
                            JwtClaimNames.SecurityStamp);

                    if (string.IsNullOrWhiteSpace(userId) ||
                        string.IsNullOrWhiteSpace(
                            tokenSecurityStamp))
                    {
                        context.Fail(
                            "The access token is missing required account claims.");

                        return;
                    }

                    var userManager =
                        context.HttpContext.RequestServices
                            .GetRequiredService<
                                UserManager<ApplicationUser>>();

                    var user =
                        await userManager.FindByIdAsync(userId);

                    if (user is null || !user.IsActive)
                    {
                        context.Fail(
                            "The account is unavailable.");

                        return;
                    }

                    var currentSecurityStamp =
                        await userManager
                            .GetSecurityStampAsync(user);

                    if (!string.Equals(
                            currentSecurityStamp,
                            tokenSecurityStamp,
                            StringComparison.Ordinal))
                    {
                        context.Fail(
                            "The access token is no longer valid.");
                    }
                }
            };
        });

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

builder.Services.AddSingleton<
    IReorderMessagePublisher,
    ReorderMessagePublisher>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }
    else
    {
        dbContext.Database.EnsureCreated();
    }

    var identityBootstrapper =
    scope.ServiceProvider
        .GetRequiredService<IdentityBootstrapper>();

    await identityBootstrapper.InitializeAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();

public partial class Program
{
}