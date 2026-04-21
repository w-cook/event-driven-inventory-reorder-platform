using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.Data.Models
{
    public class InventoryItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Sku { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Quantity On Hand")]
        public int QuantityOnHand { get; set; }

        [Required]
        [Display(Name = "Reorder Threshold")]
        public int ReorderThreshold { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = string.Empty;

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Updated At")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        List<ReorderEvent> ReorderEvents { get; set; } = new();

        List<ReorderHistory> ReorderHistoryEntries { get; set; } = new();
    }
}
