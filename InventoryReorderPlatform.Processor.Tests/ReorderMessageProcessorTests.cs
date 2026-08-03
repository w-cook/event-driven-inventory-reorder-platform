using InventoryReorderPlatform.Contracts.Messages;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using InventoryReorderPlatform.Processor.Processing;
using InventoryReorderPlatform.Processor.Supplier;
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
            RequestedQuantity = 12,
            TriggeredAt = DateTime.UtcNow,
            Status = "Pending"
        };

        dbContext.ReorderEvents.Add(reorderEvent);

        await dbContext.SaveChangesAsync(cancellationToken);

        var supplierClient =
            new AcceptingSupplierOrderClient();

        var processor = new ReorderMessageProcessor(
            dbContext,
            supplierClient,
            NullLogger<ReorderMessageProcessor>.Instance);

        var message = new ReorderRequestedMessage
        {
            ReorderEventId = reorderEvent.Id,
            InventoryItemId = reorderEvent.InventoryItemId,
            Sku = "TEST-001",
            QuantityAtTrigger = reorderEvent.QuantityAtTrigger,
            RequestedQuantity = reorderEvent.RequestedQuantity,
            TriggeredAt = reorderEvent.TriggeredAt
        };

        const string messageId = "reorder-event-1";
        const string rawPayload = """{"reorderEventId":1}""";

        var result = await processor.ProcessAsync(
            message,
            messageId,
            "processor-test-correlation",
            rawPayload,
            deliveryCount: 1,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.SupplierAccepted,
            result.Outcome);

        var savedReorderEvent =
            await dbContext.ReorderEvents.SingleAsync(cancellationToken);

        Assert.Equal(ReorderEventStatuses.SupplierAccepted, savedReorderEvent.Status);

        Assert.Equal(
            message.RequestedQuantity,
            savedReorderEvent.RequestedQuantity);

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

        Assert.Equal(
            AcceptingSupplierOrderClient.SupplierOrderId,
            savedReorderEvent.SupplierOrderId);

        Assert.Equal(
            "Accepted",
            savedReorderEvent.SupplierOrderStatus);

        Assert.Equal(
            AcceptingSupplierOrderClient.AcceptedAtUtc,
            savedReorderEvent.SupplierAcceptedAtUtc);

        Assert.Null(
            savedReorderEvent.SupplierRejectionReason);

        Assert.Equal(1, supplierClient.SubmissionCount);
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
            RequestedQuantity = 12,
            TriggeredAt = DateTime.UtcNow,
            Status = "Pending"
        };

        dbContext.ReorderEvents.Add(reorderEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        var supplierClient =
            new AcceptingSupplierOrderClient();

        var processor = new ReorderMessageProcessor(
            dbContext,
            supplierClient,
            NullLogger<ReorderMessageProcessor>.Instance);

        var message = new ReorderRequestedMessage
        {
            ReorderEventId = reorderEvent.Id,
            InventoryItemId = reorderEvent.InventoryItemId,
            Sku = "TEST-001",
            QuantityAtTrigger = reorderEvent.QuantityAtTrigger,
            RequestedQuantity = reorderEvent.RequestedQuantity,
            TriggeredAt = reorderEvent.TriggeredAt
        };

        const string messageId = "reorder-event-1";
        const string rawPayload = """{"reorderEventId":1}""";

        var firstResult = await processor.ProcessAsync(
            message,
            messageId,
            "processor-test-correlation",
            rawPayload,
            deliveryCount: 1,
            cancellationToken);

        var secondResult = await processor.ProcessAsync(
            message,
            messageId,
            "processor-test-correlation",
            rawPayload,
            deliveryCount: 2,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.SupplierAccepted,
            firstResult.Outcome);

        Assert.Equal(
            ReorderProcessingOutcome.DuplicateSkipped,
            secondResult.Outcome);

        var reorderEvents = await dbContext.ReorderEvents
            .ToListAsync(cancellationToken);

        var processedMessages = await dbContext.ProcessedMessages
            .ToListAsync(cancellationToken);

        Assert.Single(reorderEvents);
        Assert.Equal(ReorderEventStatuses.SupplierAccepted, reorderEvents[0].Status);
        Assert.Equal(
            message.RequestedQuantity,
            reorderEvents[0].RequestedQuantity);

        Assert.Single(processedMessages);
        Assert.Equal(messageId, processedMessages[0].MessageId);

        Assert.Empty(dbContext.FailedMessages);

        Assert.Equal(1, supplierClient.SubmissionCount);
    }

    [Fact]
    public async Task ProcessAsync_WhenReorderEventDoesNotExist_RecordsFailedMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var dbContext = CreateDbContext();

        var supplierClient =
            new AcceptingSupplierOrderClient();

        var processor = new ReorderMessageProcessor(
            dbContext,
            supplierClient,
            NullLogger<ReorderMessageProcessor>.Instance);

        var message = new ReorderRequestedMessage
        {
            ReorderEventId = 999,
            InventoryItemId = 10,
            Sku = "MISSING-001",
            QuantityAtTrigger = 3,
            RequestedQuantity = 12,
            TriggeredAt = DateTime.UtcNow
        };

        const string messageId = "reorder-event-999";
        const string rawPayload = """{"reorderEventId":999}""";
        const int deliveryCount = 2;

        var result = await processor.ProcessAsync(
            message,
            messageId,
            "processor-test-correlation",
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

    [Fact]
    public async Task ProcessAsync_AfterInitialFailure_ProcessesWhenReorderEventIsRestored()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await using var dbContext = CreateDbContext();

        var supplierClient =
            new AcceptingSupplierOrderClient();

        var processor = new ReorderMessageProcessor(
            dbContext,
            supplierClient,
            NullLogger<ReorderMessageProcessor>.Instance);

        var triggeredAt = DateTime.UtcNow;

        var message = new ReorderRequestedMessage
        {
            ReorderEventId = 42,
            InventoryItemId = 10,
            Sku = "RECOVERY-001",
            QuantityAtTrigger = 3,
            RequestedQuantity = 12,
            TriggeredAt = triggeredAt
        };

        const string messageId = "reorder-event-42";
        const string rawPayload = """{"reorderEventId":42}""";

        var failedResult = await processor.ProcessAsync(
            message,
            messageId,
            "processor-test-correlation",
            rawPayload,
            deliveryCount: 1,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.Failed,
            failedResult.Outcome);

        Assert.Single(
            await dbContext.FailedMessages
                .ToListAsync(cancellationToken));

        Assert.Empty(
            await dbContext.ProcessedMessages
                .ToListAsync(cancellationToken));

        dbContext.ReorderEvents.Add(
            new ReorderEvent
            {
                Id = message.ReorderEventId,
                InventoryItemId = message.InventoryItemId,
                QuantityAtTrigger = message.QuantityAtTrigger,
                RequestedQuantity = message.RequestedQuantity,
                TriggeredAt = message.TriggeredAt,
                Status = "Pending"
            });

        await dbContext.SaveChangesAsync(
            cancellationToken);

        var recoveredResult = await processor.ProcessAsync(
            message,
            messageId,
            "processor-test-correlation",
            rawPayload,
            deliveryCount: 2,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.SupplierAccepted,
            recoveredResult.Outcome);

        var reorderEvent =
            await dbContext.ReorderEvents
                .SingleAsync(cancellationToken);

        Assert.Equal(
            ReorderEventStatuses.SupplierAccepted,
            reorderEvent.Status);

        Assert.Equal(
            message.RequestedQuantity,
            reorderEvent.RequestedQuantity);

        var processedMessage =
            await dbContext.ProcessedMessages
                .SingleAsync(cancellationToken);

        Assert.Equal(
            messageId,
            processedMessage.MessageId);

        var failedMessage =
            await dbContext.FailedMessages
                .SingleAsync(cancellationToken);

        Assert.Equal(
            1,
            failedMessage.AttemptCount);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"processor-tests-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    private sealed class AcceptingSupplierOrderClient
        : ISupplierOrderClient
    {
        public static readonly Guid SupplierOrderId =
            Guid.Parse(
                "b2657c25-146c-4383-82a8-6a46d17445a9");

        public static readonly DateTime AcceptedAtUtc =
            new(
                2026,
                8,
                3,
                12,
                0,
                0,
                DateTimeKind.Utc);

        public int SubmissionCount { get; private set; }

        public Task<SupplierOrderSubmissionResult>
            SubmitOrderAsync(
                SupplierOrderRequest request,
                string idempotencyKey,
                string correlationId,
                CancellationToken cancellationToken = default)
        {
            SubmissionCount++;

            return Task.FromResult(
                SupplierOrderSubmissionResult.Accepted(
                    SupplierOrderId,
                    "Accepted",
                    AcceptedAtUtc));
        }
    }
}