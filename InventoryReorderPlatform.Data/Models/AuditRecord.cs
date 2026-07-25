using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.Data.Models
{
    public class AuditRecord
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Role { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string EntityType { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string EntityId { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Details { get; set; }

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}