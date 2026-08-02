using System.ComponentModel.DataAnnotations;

namespace InventoryReorderPlatform.SupplierMockApi.Behavior;

public sealed class SupplierBehaviorOptions
{
    public const string SectionName = "SupplierBehavior";

    public SupplierBehaviorMode Mode { get; set; } =
        SupplierBehaviorMode.Normal;

    [Range(0, 30_000)]
    public int DelayMilliseconds { get; set; } = 1_500;

    [Range(1, 20)]
    public int TransientFailuresBeforeSuccess { get; set; } = 2;

    [Required]
    [StringLength(200)]
    public string PermanentRejectionMessage { get; set; } =
        "The supplier rejected the requested order.";
}