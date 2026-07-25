using System.Security.Claims;

namespace InventoryReorderPlatform.Api.Services
{
    public interface IAuditService
    {
        Task AddRecordAsync(
            ClaimsPrincipal user,
            string action,
            string entityType,
            string entityId,
            object? details = null,
            CancellationToken cancellationToken = default);
    }
}