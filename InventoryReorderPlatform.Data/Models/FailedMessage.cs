using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.Data.Models
{
    public class FailedMessage
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string MessageId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string MessageType { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Reason { get; set; } = string.Empty;

        public string? Payload { get; set; }

        public int AttemptCount { get; set; }

        public DateTime FailedAtUtc { get; set; } = DateTime.UtcNow;
    }
}