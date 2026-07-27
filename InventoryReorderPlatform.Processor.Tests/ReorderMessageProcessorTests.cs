using InventoryReorderPlatform.Contracts.Messages;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using InventoryReorderPlatform.Processor.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryReorderPlatform.Processor.Tests;

public class ReorderMessageProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WhenMessageSucceeds_RecordsProcessedMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var dbContext = CreateDbContext();

        var reorderEvent = new ReorderEvent
        {
            Id = 1,
            InventoryItemId = 10,
            QuantityAtTrigger = 3,
            TriggeredAt = DateTime.UtcNow,
            Status = "Pending"
        };

        dbContext.ReorderEvents.Add(reorderEvent);

        await dbContext.SaveChangesAsync(cancellationToken);

        var processor = new ReorderMessageProcessor(
            dbContext,
            NullLogger<ReorderMessageProcessor>.Instance);

        var message = new ReorderRequestedMessage
        {
            ReorderEventId = reorderEvent.Id,
            InventoryItemId = reorderEvent.InventoryItemId,
            Sku = "TEST-001",
            QuantityAtTrigger = reorderEvent.QuantityAtTrigger,
            TriggeredAt = reorderEvent.TriggeredAt
        };

        const string messageId = "reorder-event-1";
        const string rawPayload = """{"reorderEventId":1}""";

        var result = await processor.ProcessAsync(
            message,
            messageId,
            rawPayload,
            deliveryCount: 1,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.Processed,
            result.Outcome);

        var savedReorderEvent =
            await dbContext.ReorderEvents.SingleAsync(cancellationToken);

        Assert.Equal("Processed", savedReorderEvent.Status);

        var processedMessage =
            await dbContext.ProcessedMessages.SingleAsync(cancellationToken);

        Assert.Equal(messageId, processedMessage.MessageId);

        Assert.Equal(
            nameof(ReorderRequestedMessage),
            processedMessage.MessageType);

        Assert.NotEqual(
            default,
            processedMessage.ProcessedAtUtc);

        Assert.Empty(dbContext.FailedMessages);
    }

    [Fact]
    public async Task ProcessAsync_WithDuplicateMessageId_DoesNotCreateDuplicateBusinessResult()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var dbContext = CreateDbContext();

        var reorderEvent = new ReorderEvent
        {
            Id = 1,
            InventoryItemId = 10,
            QuantityAtTrigger = 3,
            TriggeredAt = DateTime.UtcNow,
            Status = "Pending"
        };

        dbContext.ReorderEvents.Add(reorderEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        var processor = new ReorderMessageProcessor(
            dbContext,
            NullLogger<ReorderMessageProcessor>.Instance);

        var message = new ReorderRequestedMessage
        {
            ReorderEventId = reorderEvent.Id,
            InventoryItemId = reorderEvent.InventoryItemId,
            Sku = "TEST-001",
            QuantityAtTrigger = reorderEvent.QuantityAtTrigger,
            TriggeredAt = reorderEvent.TriggeredAt
        };

        const string messageId = "reorder-event-1";
        const string rawPayload = """{"reorderEventId":1}""";

        var firstResult = await processor.ProcessAsync(
            message,
            messageId,
            rawPayload,
            deliveryCount: 1,
            cancellationToken);

        var secondResult = await processor.ProcessAsync(
            message,
            messageId,
            rawPayload,
            deliveryCount: 2,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.Processed,
            firstResult.Outcome);

        Assert.Equal(
            ReorderProcessingOutcome.DuplicateSkipped,
            secondResult.Outcome);

        var reorderEvents = await dbContext.ReorderEvents
            .ToListAsync(cancellationToken);

        var processedMessages = await dbContext.ProcessedMessages
            .ToListAsync(cancellationToken);

        Assert.Single(reorderEvents);
        Assert.Equal("Processed", reorderEvents[0].Status);

        Assert.Single(processedMessages);
        Assert.Equal(messageId, processedMessages[0].MessageId);

        Assert.Empty(dbContext.FailedMessages);
    }

    [Fact]
    public async Task ProcessAsync_WhenReorderEventDoesNotExist_RecordsFailedMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var dbContext = CreateDbContext();

        var processor = new ReorderMessageProcessor(
            dbContext,
            NullLogger<ReorderMessageProcessor>.Instance);

        var message = new ReorderRequestedMessage
        {
            ReorderEventId = 999,
            InventoryItemId = 10,
            Sku = "MISSING-001",
            QuantityAtTrigger = 3,
            TriggeredAt = DateTime.UtcNow
        };

        const string messageId = "reorder-event-999";
        const string rawPayload = """{"reorderEventId":999}""";
        const int deliveryCount = 2;

        var result = await processor.ProcessAsync(
            message,
            messageId,
            rawPayload,
            deliveryCount,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.Failed,
            result.Outcome);

        Assert.NotNull(result.Reason);
        Assert.NotEmpty(result.Reason);
        Assert.Contains(
            message.ReorderEventId.ToString(),
            result.Reason);

        var failedMessage =
            await dbContext.FailedMessages.SingleAsync(
                cancellationToken);

        Assert.Equal(messageId, failedMessage.MessageId);

        Assert.Equal(
            nameof(ReorderRequestedMessage),
            failedMessage.MessageType);

        Assert.Equal(rawPayload, failedMessage.Payload);
        Assert.Equal(deliveryCount, failedMessage.AttemptCount);

        Assert.NotEmpty(failedMessage.Reason);

        Assert.NotEqual(
            default,
            failedMessage.FailedAtUtc);

        Assert.Empty(dbContext.ProcessedMessages);
        Assert.Empty(dbContext.ReorderEvents);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"processor-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }
}