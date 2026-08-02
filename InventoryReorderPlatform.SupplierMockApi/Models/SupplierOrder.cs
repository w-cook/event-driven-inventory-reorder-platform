using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.SupplierMockApi.Models;

public sealed class SupplierOrder
{
    public Guid Id { get; set; }

    [Required]
    [StringLength(200)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    public int ReorderEventId { get; set; }

    [Required]
    public int InventoryItemId { get; set; }

    [Required]
    [StringLength(50)]
    public string Sku { get; set; } = string.Empty;

    [Required]
    public int RequestedQuantity { get; set; }

    [Required]
    public DateTime TriggeredAtUtc { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = string.Empty;

    [Required]
    public DateTime AcceptedAtUtc { get; set; }
}