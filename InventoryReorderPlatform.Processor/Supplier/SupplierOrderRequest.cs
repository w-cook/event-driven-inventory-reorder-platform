namespace InventoryReorderPlatform.Processor.Supplier;

public sealed class SupplierOrderRequest
{
    public int ReorderEventId { get; set; }

    public int InventoryItemId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int RequestedQuantity { get; set; }

    public DateTime TriggeredAtUtc { get; set; }
}