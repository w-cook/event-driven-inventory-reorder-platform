using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;

namespace InventoryReorderPlatform.Data.Models
{
    public class ReorderEvent
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Inventory Item Id")]
        public int InventoryItemId { get; set; }

        public InventoryItem? InventoryItem { get; set; }

        [Required]
        [Display(Name = "Quantity At Trigger")]
        public int QuantityAtTrigger { get; set; }

        [Required]
        [Display(Name = "Requested Quantity")]
        public int RequestedQuantity { get; set; }

        [Display(Name = "Triggered At")]
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty;
    }
}
