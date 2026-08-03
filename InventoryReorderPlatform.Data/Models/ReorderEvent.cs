using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.Data.Models;

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
    public string Status { get; set; } =
        ReorderEventStatuses.Pending;

    [Display(Name = "Supplier Order Id")]
    public Guid? SupplierOrderId { get; set; }

    [StringLength(50)]
    [Display(Name = "Supplier Order Status")]
    public string? SupplierOrderStatus { get; set; }

    [Display(Name = "Supplier Accepted At")]
    public DateTime? SupplierAcceptedAtUtc { get; set; }

    [StringLength(1000)]
    [Display(Name = "Supplier Rejection Reason")]
    public string? SupplierRejectionReason { get; set; }
}