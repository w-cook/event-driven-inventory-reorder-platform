namespace InventoryReorderPlatform.SupplierMockApi.Behavior;

public sealed record SupplierBehaviorResult(
    SupplierBehaviorOutcome Outcome,
    string? Message = null,
    int? AttemptNumber = null);