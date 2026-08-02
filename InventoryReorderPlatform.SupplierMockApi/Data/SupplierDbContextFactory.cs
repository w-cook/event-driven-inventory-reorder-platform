using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InventoryReorderPlatform.SupplierMockApi.Data;

public sealed class SupplierDbContextFactory
    : IDesignTimeDbContextFactory<SupplierDbContext>
{
    public SupplierDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__supplierdb")
            ?? "Server=(localdb)\\MSSQLLocalDB;" +
               "Database=InventoryReorderPlatformSupplierDesign;" +
               "Trusted_Connection=True;" +
               "TrustServerCertificate=True;";

        var optionsBuilder =
            new DbContextOptionsBuilder<SupplierDbContext>();

        optionsBuilder.UseSqlServer(connectionString);

        return new SupplierDbContext(optionsBuilder.Options);
    }
}