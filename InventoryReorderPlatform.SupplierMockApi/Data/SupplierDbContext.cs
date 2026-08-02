using InventoryReorderPlatform.SupplierMockApi.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.SupplierMockApi.Data;

public sealed class SupplierDbContext
    : DbContext
{
    public SupplierDbContext(
        DbContextOptions<SupplierDbContext> options)
        : base(options)
    {
    }

    public DbSet<SupplierOrder> SupplierOrders =>
        Set<SupplierOrder>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SupplierOrder>()
            .HasIndex(order => order.IdempotencyKey)
            .IsUnique();
    }
}