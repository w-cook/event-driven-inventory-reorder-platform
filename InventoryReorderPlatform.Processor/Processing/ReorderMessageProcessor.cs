using InventoryReorderPlatform.Contracts.Messages;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Processor.Processing;

public sealed class ReorderMessageProcessor : IReorderMessageProcessor
{
    private const string MessageType = nameof(ReorderRequestedMessage);

    private readonly AppDbContext _dbContext;
    private readonly ILogger<ReorderMessageProcessor> _logger;

    public ReorderMessageProcessor(
        AppDbContext dbContext,
        ILogger<ReorderMessageProcessor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ReorderProcessingResult> ProcessAsync(
        ReorderRequestedMessage message,
        string messageId,
        string? rawPayload,
        int deliveryCount,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return await RecordFailureAsync(
                "The Service Bus message did not contain a message id.",
                "missing-message-id",
                rawPayload,
                deliveryCount,
                cancellationToken);
        }

        var alreadyProcessed = await _dbContext.ProcessedMessages
            .AsNoTracking()
            .AnyAsync(
                processedMessage =>
                    processedMessage.MessageId == messageId &&
                    processedMessage.MessageType == MessageType,
                cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Skipping duplicate reorder message {MessageId}.",
                messageId);

            return new ReorderProcessingResult(
                ReorderProcessingOutcome.DuplicateSkipped);
        }

        try
        {
            var reorderEvent = await _dbContext.ReorderEvents
                .FirstOrDefaultAsync(
                    reorderEvent => reorderEvent.Id == message.ReorderEventId,
                    cancellationToken);

            if (reorderEvent == null)
            {
                return await RecordFailureAsync(
                    $"Reorder event {message.ReorderEventId} was not found.",
                    messageId,
                    rawPayload,
                    deliveryCount,
                    cancellationToken);
            }

            if (reorderEvent.Status == "Processed")
            {
                _logger.LogInformation(
                    "Reorder event {ReorderEventId} was already processed.",
                    reorderEvent.Id);

                return new ReorderProcessingResult(
                    ReorderProcessingOutcome.DuplicateSkipped);
            }

            if (reorderEvent.Status != "Pending")
            {
                return await RecordFailureAsync(
                    $"Reorder event {reorderEvent.Id} has unsupported status " +
                    $"'{reorderEvent.Status}'.",
                    messageId,
                    rawPayload,
                    deliveryCount,
                    cancellationToken);
            }

            // In a production system, this is where the processor would call an
            // external supplier or purchasing API to submit the reorder request.
            // The reorder event should be marked as Processed only after that
            // external operation succeeds or is durably accepted. The stable
            // message id could also be supplied as an idempotency key to prevent
            // duplicate supplier orders during message retries.

            reorderEvent.Status = "Processed";

            _dbContext.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = messageId,
                MessageType = MessageType,
                ProcessedAtUtc = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Processed reorder message {MessageId} for " +
                "ReorderEventId {ReorderEventId}.",
                messageId,
                reorderEvent.Id);

            return new ReorderProcessingResult(
                ReorderProcessingOutcome.Processed);
        }
        catch (DbUpdateException ex)
        {
            // SaveChanges uses a transaction, so a unique-index failure rolls
            // back both the ledger insert and the associated status update.
            _dbContext.ChangeTracker.Clear();

            var duplicateWasCommitted = await _dbContext.ProcessedMessages
                .AsNoTracking()
                .AnyAsync(
                    processedMessage =>
                        processedMessage.MessageId == messageId &&
                        processedMessage.MessageType == MessageType,
                    cancellationToken);

            if (duplicateWasCommitted)
            {
                _logger.LogInformation(
                    ex,
                    "Concurrent duplicate reorder message {MessageId} was skipped.",
                    messageId);

                return new ReorderProcessingResult(
                    ReorderProcessingOutcome.DuplicateSkipped);
            }

            return await RecordFailureAsync(
                ex.GetBaseException().Message,
                messageId,
                rawPayload,
                deliveryCount,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _dbContext.ChangeTracker.Clear();

            return await RecordFailureAsync(
                ex.GetBaseException().Message,
                messageId,
                rawPayload,
                deliveryCount,
                cancellationToken);
        }
    }

    private async Task<ReorderProcessingResult> RecordFailureAsync(
        string reason,
        string messageId,
        string? rawPayload,
        int deliveryCount,
        CancellationToken cancellationToken)
    {
        // Prevent any partially tracked business changes from being saved
        // with the failure record.
        _dbContext.ChangeTracker.Clear();

        _dbContext.FailedMessages.Add(new FailedMessage
        {
            MessageId = messageId,
            MessageType = MessageType,
            Reason = reason,
            Payload = rawPayload,
            AttemptCount = deliveryCount,
            FailedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Reorder message {MessageId} failed on delivery attempt " +
            "{DeliveryCount}: {Reason}",
            messageId,
            deliveryCount,
            reason);

        return new ReorderProcessingResult(
            ReorderProcessingOutcome.Failed,
            reason);
    }
}