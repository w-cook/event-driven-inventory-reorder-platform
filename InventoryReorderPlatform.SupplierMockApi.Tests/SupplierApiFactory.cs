using InventoryReorderPlatform.SupplierMockApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InventoryReorderPlatform.SupplierMockApi.Tests;

public sealed class SupplierApiFactory
    : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection =
        new("Data Source=:memory:");

    public SupplierApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting(
            "ConnectionStrings:supplierdb",
            "Server=localhost;" +
            "Database=SupplierTestsUnused;" +
            "User Id=unused;" +
            "Password=unused;" +
            "TrustServerCertificate=True;");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<SupplierDbContext>();

            services.RemoveAll<
                DbContextOptions<SupplierDbContext>>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<
                    SupplierDbContext>>();

            services.AddDbContext<SupplierDbContext>(
                options =>
                    options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}