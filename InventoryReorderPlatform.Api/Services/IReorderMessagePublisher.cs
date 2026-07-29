using InventoryReorderPlatform.Contracts.Messages;

namespace InventoryReorderPlatform.Api.Services;

public interface IReorderMessagePublisher
{
    Task PublishAsync(
        ReorderRequestedMessage message,
        CancellationToken cancellationToken = default);
}