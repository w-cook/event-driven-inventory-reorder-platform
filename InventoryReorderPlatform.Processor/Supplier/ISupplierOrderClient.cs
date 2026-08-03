namespace InventoryReorderPlatform.Processor.Supplier;

public interface ISupplierOrderClient
{
    Task<SupplierOrderSubmissionResult> SubmitOrderAsync(
        SupplierOrderRequest request,
        string idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken = default);
}