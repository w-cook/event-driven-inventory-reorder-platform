using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.SupplierMockApi.Contracts;

public sealed class CreateSupplierOrderRequest
{
    [Range(1, int.MaxValue)]
    public int ReorderEventId { get; set; }

    [Range(1, int.MaxValue)]
    public int InventoryItemId { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Sku { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int RequestedQuantity { get; set; }

    public DateTime TriggeredAtUtc { get; set; }
}