using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.Api.DTOs
{
    public class UpdateInventoryItemRequest
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Sku { get; set; } = string.Empty;

        [Required]
        [Range(0, int.MaxValue)]
        public int QuantityOnHand { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int ReorderThreshold { get; set; }
    }
}
