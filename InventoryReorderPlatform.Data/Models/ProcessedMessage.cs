using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.Data.Models
{
    public class ProcessedMessage
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string MessageId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string MessageType { get; set; } = string.Empty;

        public DateTime ProcessedAtUtc { get; set; } = DateTime.UtcNow;
    }
}