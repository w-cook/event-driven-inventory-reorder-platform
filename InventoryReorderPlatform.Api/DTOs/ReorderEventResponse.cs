namespace InventoryReorderPlatform.Api.DTOs;

public class ReorderEventResponse
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }

    public int QuantityAtTrigger { get; set; }

    public int RequestedQuantity { get; set; }

    public DateTime TriggeredAt { get; set; } =
        DateTime.UtcNow;

    public string Status { get; set; } = string.Empty;

    public Guid? SupplierOrderId { get; set; }

    public string? SupplierOrderStatus { get; set; }

    public DateTime? SupplierAcceptedAtUtc { get; set; }

    public string? SupplierRejectionReason { get; set; }
}