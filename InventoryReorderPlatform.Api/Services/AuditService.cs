using System.Security.Claims;
using System.Text.Json;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;

namespace InventoryReorderPlatform.Api.Services
{
    public class AuditService : IAuditService
    {
        private readonly AppDbContext _dbContext;

        public AuditService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddRecordAsync(
            ClaimsPrincipal user,
            string action,
            string entityType,
            string entityId,
            object? details = null,
            CancellationToken cancellationToken = default)
        {
            var userName =
                user.Identity?.Name
                ?? user.FindFirst(ClaimTypes.Name)?.Value
                ?? user.FindFirst(ClaimTypes.Email)?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? "Unknown";

            var role =
                user.FindFirst(ClaimTypes.Role)?.Value
                ?? "Unknown";

            var serializedDetails = details == null
                ? null
                : JsonSerializer.Serialize(details);

            var auditRecord = new AuditRecord
            {
                UserName = userName,
                Role = role,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = serializedDetails,
                OccurredAt = DateTime.UtcNow
            };

            await _dbContext.AuditRecords.AddAsync(
                auditRecord,
                cancellationToken);
        }
    }
}