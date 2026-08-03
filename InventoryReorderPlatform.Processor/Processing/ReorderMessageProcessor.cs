using InventoryReorderPlatform.Contracts.Messages;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using InventoryReorderPlatform.Processor.Supplier;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Processor.Processing;

public sealed class ReorderMessageProcessor
    : IReorderMessageProcessor
{
    private const string MessageType =
        nameof(ReorderRequestedMessage);

    private const int MaximumSupplierStatusLength = 50;
    private const int MaximumRejectionReasonLength = 1000;

    private readonly AppDbContext _dbContext;
    private readonly ISupplierOrderClient _supplierOrderClient;
    private readonly ILogger<ReorderMessageProcessor> _logger;

    public ReorderMessageProcessor(
        AppDbContext dbContext,
        ISupplierOrderClient supplierOrderClient,
        ILogger<ReorderMessageProcessor> logger)
    {
        _dbContext = dbContext;
        _supplierOrderClient = supplierOrderClient;
        _logger = logger;
    }

    public async Task<ReorderProcessingResult> ProcessAsync(
        ReorderRequestedMessage message,
        string messageId,
        string correlationId,
        string? rawPayload,
        int deliveryCount,
        CancellationToken cancellationToken = default)
    {
        var logCorrelationId =
            string.IsNullOrWhiteSpace(correlationId)
                ? "missing"
                : correlationId;

        using var loggingScope =
            _logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["CorrelationId"] = logCorrelationId,
                    ["MessageId"] =
                        string.IsNullOrWhiteSpace(messageId)
                            ? "missing-message-id"
                            : messageId
                });

        if (string.IsNullOrWhiteSpace(messageId))
        {
            return await RecordFailureAsync(
                "The Service Bus message did not contain a message id.",
                "missing-message-id",
                logCorrelationId,
                rawPayload,
                deliveryCount,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return await RecordFailureAsync(
                "The Service Bus message did not contain a valid " +
                "correlation identifier.",
                messageId,
                logCorrelationId,
                rawPayload,
                deliveryCount,
                cancellationToken);
        }

        var alreadyProcessed =
            await _dbContext.ProcessedMessages
                .AsNoTracking()
                .AnyAsync(
                    processedMessage =>
                        processedMessage.MessageId == messageId &&
                        processedMessage.MessageType == MessageType,
                    cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Skipping duplicate reorder message {MessageId} " +
                "with CorrelationId {CorrelationId}.",
                messageId,
                logCorrelationId);

            return new ReorderProcessingResult(
                ReorderProcessingOutcome.DuplicateSkipped);
        }

        try
        {
            var reorderEvent =
                await _dbContext.ReorderEvents
                    .FirstOrDefaultAsync(
                        item =>
                            item.Id == message.ReorderEventId,
                        cancellationToken);

            if (reorderEvent is null)
            {
                return await RecordFailureAsync(
                    $"Reorder event {message.ReorderEventId} " +
                    "was not found.",
                    messageId,
                    logCorrelationId,
                    rawPayload,
                    deliveryCount,
                    cancellationToken);
            }

            if (ReorderEventStatuses.IsTerminal(
                    reorderEvent.Status))
            {
                _logger.LogInformation(
                    "Reorder event {ReorderEventId} already has " +
                    "terminal status {ReorderStatus} with " +
                    "CorrelationId {CorrelationId}.",
                    reorderEvent.Id,
                    reorderEvent.Status,
                    logCorrelationId);

                return new ReorderProcessingResult(
                    ReorderProcessingOutcome.DuplicateSkipped);
            }

            if (reorderEvent.Status !=
                ReorderEventStatuses.Pending)
            {
                return await RecordFailureAsync(
                    $"Reorder event {reorderEvent.Id} has " +
                    $"unsupported status '{reorderEvent.Status}'.",
                    messageId,
                    logCorrelationId,
                    rawPayload,
                    deliveryCount,
                    cancellationToken);
            }

            var supplierRequest = new SupplierOrderRequest
            {
                ReorderEventId = message.ReorderEventId,
                InventoryItemId = message.InventoryItemId,
                Sku = message.Sku,
                RequestedQuantity =
                    message.RequestedQuantity,
                TriggeredAtUtc =
                    EnsureUtc(message.TriggeredAt)
            };

            var supplierResult =
                await _supplierOrderClient.SubmitOrderAsync(
                    supplierRequest,
                    idempotencyKey: messageId,
                    correlationId,
                    cancellationToken);

            ReorderProcessingOutcome processingOutcome;
            string? terminalReason = null;

            switch (supplierResult.Outcome)
            {
                case SupplierOrderSubmissionOutcome.Accepted:
                    ApplySupplierAcceptance(
                        reorderEvent,
                        supplierResult);

                    processingOutcome =
                        ReorderProcessingOutcome
                            .SupplierAccepted;

                    break;

                case SupplierOrderSubmissionOutcome.Rejected:
                    terminalReason =
                        ApplySupplierRejection(
                            reorderEvent,
                            supplierResult);

                    processingOutcome =
                        ReorderProcessingOutcome
                            .SupplierRejected;

                    break;

                default:
                    throw new InvalidOperationException(
                        "The supplier client returned an " +
                        $"unsupported outcome: " +
                        $"{supplierResult.Outcome}.");
            }

            _dbContext.ProcessedMessages.Add(
                new ProcessedMessage
                {
                    MessageId = messageId,
                    MessageType = MessageType,
                    ProcessedAtUtc = DateTime.UtcNow
                });

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            _logger.LogInformation(
                "Handled reorder message {MessageId} for " +
                "ReorderEventId {ReorderEventId} with terminal " +
                "status {ReorderStatus} and CorrelationId " +
                "{CorrelationId}.",
                messageId,
                reorderEvent.Id,
                reorderEvent.Status,
                logCorrelationId);

            return new ReorderProcessingResult(
                processingOutcome,
                terminalReason);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateException exception)
        {
            // The event update and processed-message insert are
            // committed together. A unique-index conflict therefore
            // rolls back both local changes.
            _dbContext.ChangeTracker.Clear();

            var duplicateWasCommitted =
                await _dbContext.ProcessedMessages
                    .AsNoTracking()
                    .AnyAsync(
                        processedMessage =>
                            processedMessage.MessageId ==
                                messageId &&
                            processedMessage.MessageType ==
                                MessageType,
                        cancellationToken);

            if (duplicateWasCommitted)
            {
                _logger.LogInformation(
                    exception,
                    "Concurrent duplicate reorder message " +
                    "{MessageId} with CorrelationId " +
                    "{CorrelationId} was skipped.",
                    messageId,
                    logCorrelationId);

                return new ReorderProcessingResult(
                    ReorderProcessingOutcome
                        .DuplicateSkipped);
            }

            return await RecordFailureAsync(
                exception.GetBaseException().Message,
                messageId,
                logCorrelationId,
                rawPayload,
                deliveryCount,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _dbContext.ChangeTracker.Clear();

            return await RecordFailureAsync(
                exception.GetBaseException().Message,
                messageId,
                logCorrelationId,
                rawPayload,
                deliveryCount,
                cancellationToken);
        }
    }

    private static void ApplySupplierAcceptance(
        ReorderEvent reorderEvent,
        SupplierOrderSubmissionResult supplierResult)
    {
        if (supplierResult.SupplierOrderId is not Guid
                supplierOrderId ||
            supplierOrderId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "An accepted supplier result must contain a " +
                "supplier order identifier.");
        }

        var supplierStatus =
            supplierResult.SupplierOrderStatus?.Trim();

        if (string.IsNullOrWhiteSpace(supplierStatus))
        {
            throw new InvalidOperationException(
                "An accepted supplier result must contain a " +
                "supplier order status.");
        }

        if (supplierStatus.Length >
            MaximumSupplierStatusLength)
        {
            throw new InvalidOperationException(
                "The supplier order status exceeds the " +
                "supported length.");
        }

        if (supplierResult.AcceptedAtUtc is not DateTime
            acceptedAtUtc ||
            acceptedAtUtc == default)
        {
            throw new InvalidOperationException(
                "An accepted supplier result must contain an " +
                "acceptance timestamp.");
        }

        reorderEvent.Status =
            ReorderEventStatuses.SupplierAccepted;

        reorderEvent.SupplierOrderId = supplierOrderId;
        reorderEvent.SupplierOrderStatus = supplierStatus;

        reorderEvent.SupplierAcceptedAtUtc =
            EnsureUtc(acceptedAtUtc);

        reorderEvent.SupplierRejectionReason = null;
    }

    private static string ApplySupplierRejection(
        ReorderEvent reorderEvent,
        SupplierOrderSubmissionResult supplierResult)
    {
        var supplierStatus =
            string.IsNullOrWhiteSpace(
                supplierResult.SupplierOrderStatus)
                ? "Rejected"
                : supplierResult.SupplierOrderStatus.Trim();

        if (supplierStatus.Length >
            MaximumSupplierStatusLength)
        {
            throw new InvalidOperationException(
                "The supplier order status exceeds the " +
                "supported length.");
        }

        var rejectionReason =
            NormalizeRejectionReason(
                supplierResult.RejectionReason);

        reorderEvent.Status =
            ReorderEventStatuses.SupplierRejected;

        reorderEvent.SupplierOrderId = null;
        reorderEvent.SupplierOrderStatus = supplierStatus;
        reorderEvent.SupplierAcceptedAtUtc = null;

        reorderEvent.SupplierRejectionReason =
            rejectionReason;

        return rejectionReason;
    }

    private static string NormalizeRejectionReason(
        string? rejectionReason)
    {
        var normalized =
            string.IsNullOrWhiteSpace(rejectionReason)
                ? "The supplier permanently rejected the order."
                : rejectionReason.Trim();

        if (normalized.Length >
            MaximumRejectionReasonLength)
        {
            normalized =
                normalized[..MaximumRejectionReasonLength];
        }

        return normalized;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc)
        };
    }

    private async Task<ReorderProcessingResult> RecordFailureAsync(
        string reason,
        string messageId,
        string correlationId,
        string? rawPayload,
        int deliveryCount,
        CancellationToken cancellationToken)
    {
        // Do not save partially tracked supplier-state changes
        // alongside a retryable failure record.
        _dbContext.ChangeTracker.Clear();

        _dbContext.FailedMessages.Add(
            new FailedMessage
            {
                MessageId = messageId,
                MessageType = MessageType,
                Reason = reason,
                Payload = rawPayload,
                AttemptCount = deliveryCount,
                FailedAtUtc = DateTime.UtcNow
            });

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        _logger.LogWarning(
            "Reorder message {MessageId} with CorrelationId " +
            "{CorrelationId} failed on delivery attempt " +
            "{DeliveryCount}: {Reason}",
            messageId,
            correlationId,
            deliveryCount,
            reason);

        return new ReorderProcessingResult(
            ReorderProcessingOutcome.Failed,
            reason);
    }
}