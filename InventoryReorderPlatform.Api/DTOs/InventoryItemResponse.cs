namespace InventoryReorderPlatform.Api.DTOs
{
    public class InventoryItemResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Sku { get; set; } = string.Empty;
        public int QuantityOnHand { get; set; }
        public int ReorderThreshold { get; set; }
        public int ReorderQuantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
