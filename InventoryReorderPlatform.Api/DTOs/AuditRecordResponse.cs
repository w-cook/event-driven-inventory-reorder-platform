namespace InventoryReorderPlatform.Api.DTOs
{
    public class AuditRecordResponse
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public string EntityId { get; set; } = string.Empty;

        public string? Details { get; set; }

        public DateTime OccurredAt { get; set; }
    }
}