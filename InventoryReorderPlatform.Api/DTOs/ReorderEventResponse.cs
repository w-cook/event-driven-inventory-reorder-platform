using System.ComponentModel.DataAnnotations;
using InventoryReorderPlatform.Data.Models;

namespace InventoryReorderPlatform.Api.DTOs
{
    public class ReorderEventResponse
    {
        public int Id { get; set; }
        public int InventoryItemId { get; set; }
        public InventoryItem? InventoryItem { get; set; }
        public int QuantityAtTrigger { get; set; }
        public int RequestedQuantity { get; set; }
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = string.Empty;
    }
}
