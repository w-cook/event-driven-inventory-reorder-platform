using InventoryReorderPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

        public DbSet<ReorderEvent> ReorderEvents => Set<ReorderEvent>();

        public DbSet<ReorderHistory> ReorderHistoryEntries => Set<ReorderHistory>();
    }
}
