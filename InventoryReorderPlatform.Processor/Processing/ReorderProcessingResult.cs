namespace InventoryReorderPlatform.Processor.Processing;

public sealed record ReorderProcessingResult(
    ReorderProcessingOutcome Outcome,
    string? Reason = null);