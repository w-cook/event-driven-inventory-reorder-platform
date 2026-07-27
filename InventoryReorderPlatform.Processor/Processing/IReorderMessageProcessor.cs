using InventoryReorderPlatform.Contracts.Messages;

namespace InventoryReorderPlatform.Processor.Processing;

public interface IReorderMessageProcessor
{
    Task<ReorderProcessingResult> ProcessAsync(
        ReorderRequestedMessage message,
        string messageId,
        string? rawPayload,
        int deliveryCount,
        CancellationToken cancellationToken = default);
}