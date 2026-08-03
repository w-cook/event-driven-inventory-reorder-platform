namespace InventoryReorderPlatform.Processor.Supplier;

public sealed class SupplierOrderResponse
{
    public Guid SupplierOrderId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public int ReorderEventId { get; set; }

    public int InventoryItemId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public int RequestedQuantity { get; set; }

    public DateTime TriggeredAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime AcceptedAtUtc { get; set; }
}