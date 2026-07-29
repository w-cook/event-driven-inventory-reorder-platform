using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace InventoryReorderPlatform.Api.Tests;

public sealed class InventoryApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _databaseName =
        $"inventory-api-tests-{Guid.NewGuid()}";

    private readonly CapturingReorderMessagePublisher
    _messagePublisher = new();

    public CapturingReorderMessagePublisher MessagePublisher =>
        _messagePublisher;

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.UseSetting(
            "ServiceBus:ConnectionString",
            "Endpoint=sb://localhost/;" +
            "SharedAccessKeyName=RootManageSharedAccessKey;" +
            "SharedAccessKey=SAS_KEY_VALUE;" +
            "UseDevelopmentEmulator=true;");

        builder.UseSetting(
            "ServiceBus:QueueName",
            "reorder-events");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();

            services.RemoveAll<
                DbContextOptions<AppDbContext>>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContextPool<AppDbContext>(
                options =>
                {
                    options.UseInMemoryDatabase(
                        _databaseName);
                });

            services.RemoveAll<IReorderMessagePublisher>();
            services.RemoveAll<ReorderMessagePublisher>();

            services.AddSingleton<IReorderMessagePublisher>(
                _messagePublisher);
        });
    }
}