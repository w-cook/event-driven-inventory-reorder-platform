namespace InventoryReorderPlatform.SupplierMockApi.Behavior;

public interface ISupplierBehaviorSimulator
{
    Task<SupplierBehaviorResult> EvaluateAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);
}