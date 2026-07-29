using InventoryReorderPlatform.Data.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Data
{
    public class AppDbContext
        : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

        public DbSet<ReorderEvent> ReorderEvents => Set<ReorderEvent>();

        public DbSet<ReorderHistory> ReorderHistoryEntries => Set<ReorderHistory>();

        public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

        public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

        public DbSet<FailedMessage> FailedMessages => Set<FailedMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProcessedMessage>()
                .HasIndex(message => new
                {
                    message.MessageId,
                    message.MessageType
                })
                .IsUnique();
        }
    }
}
