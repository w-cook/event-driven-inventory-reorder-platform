using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;

namespace InventoryReorderPlatform.Data.Models
{
    public class ReorderHistory
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Inventory Item Id")]
        public int InventoryItemId { get; set; }

        public InventoryItem? InventoryItem { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Old Status")]
        public string OldStatus { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Display(Name = "New Status")]
        public string NewStatus { get; set; } = string.Empty;

        [Display(Name = "Changed At")]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}
