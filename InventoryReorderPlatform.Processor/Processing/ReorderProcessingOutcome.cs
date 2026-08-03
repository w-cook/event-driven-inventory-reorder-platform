namespace InventoryReorderPlatform.Processor.Processing;

public enum ReorderProcessingOutcome
{
    SupplierAccepted,
    SupplierRejected,
    DuplicateSkipped,
    Failed
}