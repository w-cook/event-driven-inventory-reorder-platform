using System.Collections.Concurrent;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Contracts.Messages;

namespace InventoryReorderPlatform.Api.Tests;

public sealed class CapturingReorderMessagePublisher
    : IReorderMessagePublisher
{
    private readonly ConcurrentQueue<ReorderRequestedMessage>
        _messages = new();

    public IReadOnlyList<ReorderRequestedMessage> Messages =>
        _messages.ToArray();

    public Task PublishAsync(
        ReorderRequestedMessage message,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(message);

        return Task.CompletedTask;
    }

    public void Clear()
    {
        _messages.Clear();
    }
}