using System.Net;
using InventoryReorderPlatform.Contracts.Messages;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using InventoryReorderPlatform.Processor.Processing;
using InventoryReorderPlatform.Processor.Supplier;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryReorderPlatform.Processor.Tests;

public sealed class SupplierWorkflowProcessingTests
{
    [Fact]
    public async Task ProcessAsync_WhenSupplierRejects_PersistsTerminalRejection()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await using var dbContext = CreateDbContext();

        var reorderEvent = CreatePendingReorderEvent();

        dbContext.ReorderEvents.Add(reorderEvent);

        await dbContext.SaveChangesAsync(cancellationToken);

        const string rejectionReason =
            "The requested SKU is unavailable.";

        var supplierClient =
            new RejectingSupplierOrderClient(
                rejectionReason);

        var processor = CreateProcessor(
            dbContext,
            supplierClient);

        var message = CreateMessage(reorderEvent);

        const string messageId = "reorder-event-1";
        const string correlationId =
            "supplier-rejection-correlation";

        var result = await processor.ProcessAsync(
            message,
            messageId,
            correlationId,
            """{"reorderEventId":1}""",
            deliveryCount: 1,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.SupplierRejected,
            result.Outcome);

        Assert.Equal(
            rejectionReason,
            result.Reason);

        var savedEvent =
            await dbContext.ReorderEvents
                .AsNoTracking()
                .SingleAsync(cancellationToken);

        Assert.Equal(
            ReorderEventStatuses.SupplierRejected,
            savedEvent.Status);

        Assert.Null(savedEvent.SupplierOrderId);

        Assert.Equal(
            "Rejected",
            savedEvent.SupplierOrderStatus);

        Assert.Null(
            savedEvent.SupplierAcceptedAtUtc);

        Assert.Equal(
            rejectionReason,
            savedEvent.SupplierRejectionReason);

        var processedMessage =
            await dbContext.ProcessedMessages
                .AsNoTracking()
                .SingleAsync(cancellationToken);

        Assert.Equal(
            messageId,
            processedMessage.MessageId);

        Assert.Empty(
            await dbContext.FailedMessages
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        Assert.Equal(
            1,
            supplierClient.SubmissionCount);

        Assert.Equal(
            messageId,
            supplierClient.LastIdempotencyKey);

        Assert.Equal(
            correlationId,
            supplierClient.LastCorrelationId);
    }

    [Fact]
    public async Task ProcessAsync_AfterTransientSupplierFailure_RecoversOnRedelivery()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await using var dbContext = CreateDbContext();

        var reorderEvent = CreatePendingReorderEvent();

        dbContext.ReorderEvents.Add(reorderEvent);

        await dbContext.SaveChangesAsync(cancellationToken);

        var supplierClient =
            new TransientThenAcceptingSupplierOrderClient();

        var processor = CreateProcessor(
            dbContext,
            supplierClient);

        var message = CreateMessage(reorderEvent);

        const string messageId = "reorder-event-1";
        const string correlationId =
            "transient-recovery-correlation";

        var firstResult = await processor.ProcessAsync(
            message,
            messageId,
            correlationId,
            """{"reorderEventId":1}""",
            deliveryCount: 1,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.Failed,
            firstResult.Outcome);

        var pendingEvent =
            await dbContext.ReorderEvents
                .AsNoTracking()
                .SingleAsync(cancellationToken);

        Assert.Equal(
            ReorderEventStatuses.Pending,
            pendingEvent.Status);

        Assert.Null(pendingEvent.SupplierOrderId);
        Assert.Null(pendingEvent.SupplierOrderStatus);

        Assert.Empty(
            await dbContext.ProcessedMessages
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        var firstFailure =
            await dbContext.FailedMessages
                .AsNoTracking()
                .SingleAsync(cancellationToken);

        Assert.Equal(1, firstFailure.AttemptCount);

        var secondResult = await processor.ProcessAsync(
            message,
            messageId,
            correlationId,
            """{"reorderEventId":1}""",
            deliveryCount: 2,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.SupplierAccepted,
            secondResult.Outcome);

        var acceptedEvent =
            await dbContext.ReorderEvents
                .AsNoTracking()
                .SingleAsync(cancellationToken);

        Assert.Equal(
            ReorderEventStatuses.SupplierAccepted,
            acceptedEvent.Status);

        Assert.Equal(
            TransientThenAcceptingSupplierOrderClient
                .SupplierOrderId,
            acceptedEvent.SupplierOrderId);

        Assert.Equal(
            "Accepted",
            acceptedEvent.SupplierOrderStatus);

        Assert.Equal(
            TransientThenAcceptingSupplierOrderClient
                .AcceptedAtUtc,
            acceptedEvent.SupplierAcceptedAtUtc);

        Assert.Null(
            acceptedEvent.SupplierRejectionReason);

        Assert.Single(
            await dbContext.ProcessedMessages
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        Assert.Single(
            await dbContext.FailedMessages
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        Assert.Equal(
            2,
            supplierClient.SubmissionCount);

        Assert.All(
            supplierClient.IdempotencyKeys,
            key => Assert.Equal(messageId, key));

        Assert.All(
            supplierClient.CorrelationIds,
            id => Assert.Equal(correlationId, id));
    }

    [Fact]
    public async Task ProcessAsync_WhenLocalSaveFailsAfterSupplierAcceptance_RedeliveryDoesNotCreateDuplicateSupplierOrder()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        await using var dbContext =
            CreateFailOnceDbContext();

        var reorderEvent = CreatePendingReorderEvent();

        dbContext.ReorderEvents.Add(reorderEvent);

        await dbContext.SaveChangesAsync(cancellationToken);

        var supplierClient =
            new IdempotentSupplierOrderClient();

        var processor = CreateProcessor(
            dbContext,
            supplierClient);

        var message = CreateMessage(reorderEvent);

        const string messageId = "reorder-event-1";
        const string correlationId =
            "local-save-recovery-correlation";

        dbContext.FailNextSave = true;

        var firstResult = await processor.ProcessAsync(
            message,
            messageId,
            correlationId,
            """{"reorderEventId":1}""",
            deliveryCount: 1,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.Failed,
            firstResult.Outcome);

        var eventAfterFailedSave =
            await dbContext.ReorderEvents
                .AsNoTracking()
                .SingleAsync(cancellationToken);

        Assert.Equal(
            ReorderEventStatuses.Pending,
            eventAfterFailedSave.Status);

        Assert.Empty(
            await dbContext.ProcessedMessages
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        Assert.Equal(
            1,
            supplierClient.SubmissionCount);

        Assert.Equal(
            1,
            supplierClient.UniqueOrderCount);

        var secondResult = await processor.ProcessAsync(
            message,
            messageId,
            correlationId,
            """{"reorderEventId":1}""",
            deliveryCount: 2,
            cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.SupplierAccepted,
            secondResult.Outcome);

        var savedEvent =
            await dbContext.ReorderEvents
                .AsNoTracking()
                .SingleAsync(cancellationToken);

        Assert.Equal(
            ReorderEventStatuses.SupplierAccepted,
            savedEvent.Status);

        Assert.Equal(
            IdempotentSupplierOrderClient.SupplierOrderId,
            savedEvent.SupplierOrderId);

        Assert.Single(
            await dbContext.ProcessedMessages
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        Assert.Single(
            await dbContext.FailedMessages
                .AsNoTracking()
                .ToListAsync(cancellationToken));

        Assert.Equal(
            2,
            supplierClient.SubmissionCount);

        Assert.Equal(
            1,
            supplierClient.UniqueOrderCount);

        Assert.All(
            supplierClient.IdempotencyKeys,
            key => Assert.Equal(messageId, key));
    }

    private static ReorderMessageProcessor CreateProcessor(
        AppDbContext dbContext,
        ISupplierOrderClient supplierClient)
    {
        return new ReorderMessageProcessor(
            dbContext,
            supplierClient,
            NullLogger<ReorderMessageProcessor>.Instance);
    }

    private static ReorderEvent
        CreatePendingReorderEvent()
    {
        return new ReorderEvent
        {
            Id = 1,
            InventoryItemId = 10,
            QuantityAtTrigger = 3,
            RequestedQuantity = 12,
            TriggeredAt = new DateTime(
                2026,
                8,
                3,
                12,
                0,
                0,
                DateTimeKind.Utc),
            Status = ReorderEventStatuses.Pending
        };
    }

    private static ReorderRequestedMessage CreateMessage(
        ReorderEvent reorderEvent)
    {
        return new ReorderRequestedMessage
        {
            ReorderEventId = reorderEvent.Id,
            InventoryItemId =
                reorderEvent.InventoryItemId,
            Sku = "SUPPLIER-WORKFLOW-001",
            QuantityAtTrigger =
                reorderEvent.QuantityAtTrigger,
            RequestedQuantity =
                reorderEvent.RequestedQuantity,
            TriggeredAt = reorderEvent.TriggeredAt
        };
    }

    private static AppDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    $"supplier-workflow-tests-" +
                    $"{Guid.NewGuid()}")
                .Options;

        return new AppDbContext(options);
    }

    private static FailOnceAppDbContext
        CreateFailOnceDbContext()
    {
        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(
                    $"supplier-save-failure-tests-" +
                    $"{Guid.NewGuid()}")
                .Options;

        return new FailOnceAppDbContext(options);
    }

    private sealed class RejectingSupplierOrderClient
        : ISupplierOrderClient
    {
        private readonly string _rejectionReason;

        public RejectingSupplierOrderClient(
            string rejectionReason)
        {
            _rejectionReason = rejectionReason;
        }

        public int SubmissionCount { get; private set; }

        public string? LastIdempotencyKey
        {
            get;
            private set;
        }

        public string? LastCorrelationId
        {
            get;
            private set;
        }

        public Task<SupplierOrderSubmissionResult>
            SubmitOrderAsync(
                SupplierOrderRequest request,
                string idempotencyKey,
                string correlationId,
                CancellationToken cancellationToken = default)
        {
            SubmissionCount++;
            LastIdempotencyKey = idempotencyKey;
            LastCorrelationId = correlationId;

            return Task.FromResult(
                SupplierOrderSubmissionResult.Rejected(
                    _rejectionReason));
        }
    }

    private sealed class
        TransientThenAcceptingSupplierOrderClient
        : ISupplierOrderClient
    {
        public static readonly Guid SupplierOrderId =
            Guid.Parse(
                "fb581f29-60fb-4b63-a201-0a458bd97c87");

        public static readonly DateTime AcceptedAtUtc =
            new(
                2026,
                8,
                3,
                12,
                15,
                0,
                DateTimeKind.Utc);

        public int SubmissionCount { get; private set; }

        public List<string> IdempotencyKeys
        {
            get;
        } = [];

        public List<string> CorrelationIds
        {
            get;
        } = [];

        public Task<SupplierOrderSubmissionResult>
            SubmitOrderAsync(
                SupplierOrderRequest request,
                string idempotencyKey,
                string correlationId,
                CancellationToken cancellationToken = default)
        {
            SubmissionCount++;
            IdempotencyKeys.Add(idempotencyKey);
            CorrelationIds.Add(correlationId);

            if (SubmissionCount == 1)
            {
                throw new HttpRequestException(
                    "The supplier is temporarily unavailable.",
                    inner: null,
                    statusCode:
                        HttpStatusCode.ServiceUnavailable);
            }

            return Task.FromResult(
                SupplierOrderSubmissionResult.Accepted(
                    SupplierOrderId,
                    "Accepted",
                    AcceptedAtUtc));
        }
    }

    private sealed class IdempotentSupplierOrderClient
        : ISupplierOrderClient
    {
        public static readonly Guid SupplierOrderId =
            Guid.Parse(
                "8a42990a-3095-4db9-b5f8-c9842404ab31");

        private readonly Dictionary<
            string,
            SupplierOrderSubmissionResult> _orders = [];

        public int SubmissionCount { get; private set; }

        public int UniqueOrderCount => _orders.Count;

        public List<string> IdempotencyKeys
        {
            get;
        } = [];

        public Task<SupplierOrderSubmissionResult>
            SubmitOrderAsync(
                SupplierOrderRequest request,
                string idempotencyKey,
                string correlationId,
                CancellationToken cancellationToken = default)
        {
            SubmissionCount++;
            IdempotencyKeys.Add(idempotencyKey);

            if (!_orders.TryGetValue(
                    idempotencyKey,
                    out var result))
            {
                result =
                    SupplierOrderSubmissionResult.Accepted(
                        SupplierOrderId,
                        "Accepted",
                        new DateTime(
                            2026,
                            8,
                            3,
                            12,
                            30,
                            0,
                            DateTimeKind.Utc));

                _orders.Add(idempotencyKey, result);
            }

            return Task.FromResult(result);
        }
    }

    private sealed class FailOnceAppDbContext
        : AppDbContext
    {
        public FailOnceAppDbContext(
            DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public bool FailNextSave { get; set; }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;

                throw new DbUpdateException(
                    "Simulated local persistence failure.");
            }

            return base.SaveChangesAsync(
                cancellationToken);
        }
    }
}