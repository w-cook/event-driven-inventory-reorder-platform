using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using InventoryReorderPlatform.Contracts.Configuration;
using InventoryReorderPlatform.Contracts.Messages;
using InventoryReorderPlatform.Observability;
using InventoryReorderPlatform.Processor.Processing;
using Microsoft.Extensions.Options;

namespace InventoryReorderPlatform.Processor;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusOptions _options;

    public Worker(
        IServiceScopeFactory scopeFactory,
        ILogger<Worker> logger,
        ServiceBusClient serviceBusClient,
        IOptions<ServiceBusOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _serviceBusClient = serviceBusClient;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Service Bus processor worker started.");

        await using var receiver =
            _serviceBusClient.CreateReceiver(_options.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await receiver.ReceiveMessagesAsync(
                    maxMessages: 5,
                    maxWaitTime: TimeSpan.FromSeconds(5),
                    cancellationToken: stoppingToken);

                if (messages.Count == 0)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(2),
                        stoppingToken);

                    continue;
                }

                foreach (var message in messages)
                {
                    await ProcessMessageAsync(
                        receiver,
                        message,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error while receiving Service Bus messages.");

                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        var correlationId = GetCorrelationId(message);

        using var activity =
            StartProcessingActivity(message);

        activity?.SetTag(
            "messaging.system",
            "servicebus");

        activity?.SetTag(
            "messaging.destination.name",
            _options.QueueName);

        activity?.SetTag(
            "messaging.message.id",
            message.MessageId);

        activity?.SetTag(
            "messaging.operation.name",
            "process");

        activity?.SetTag(
            "messaging.delivery.count",
            message.DeliveryCount);

        activity?.SetTag(
            "correlation.id",
            correlationId);

        using var loggingScope =
            _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["MessageId"] = message.MessageId,
                ["TraceId"] =
                    activity?.TraceId.ToString() ??
                    Activity.Current?.TraceId.ToString() ??
                    string.Empty
            });

        var rawPayload = message.Body.ToString();

        ReorderRequestedMessage? reorderMessage;

        try
        {
            reorderMessage =
                JsonSerializer.Deserialize<ReorderRequestedMessage>(
                    rawPayload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Message {MessageId} with CorrelationId " +
                "{CorrelationId} contained invalid JSON.",
                message.MessageId,
                correlationId);

            activity?.SetTag(
                "reorder.outcome",
                "invalid-json");

            activity?.SetTag(
                "messaging.settlement",
                "dead-lettered");

            activity?.SetStatus(
                ActivityStatusCode.Error,
                "Invalid JSON payload");

            await receiver.DeadLetterMessageAsync(
                message,
                deadLetterReason: "InvalidPayload",
                deadLetterErrorDescription:
                    "The message body could not be deserialized.",
                cancellationToken);

            return;
        }

        if (reorderMessage == null)
        {
            _logger.LogWarning(
                "Message {MessageId} with CorrelationId " +
                "{CorrelationId} did not contain a valid " +
                "reorder request.",
                message.MessageId,
                correlationId);

            activity?.SetTag(
                "reorder.outcome",
                "invalid-payload");

            activity?.SetTag(
                "messaging.settlement",
                "dead-lettered");

            activity?.SetStatus(
                ActivityStatusCode.Error,
                "Missing reorder request");

            await receiver.DeadLetterMessageAsync(
                message,
                deadLetterReason: "InvalidPayload",
                deadLetterErrorDescription:
                    "The message body did not contain a reorder request.",
                cancellationToken);

            return;
        }

        activity?.SetTag(
            "reorder.event.id",
            reorderMessage.ReorderEventId);

        activity?.SetTag(
            "inventory.item.id",
            reorderMessage.InventoryItemId);

        _logger.LogInformation(
            "Received reorder message {MessageId} for ReorderEventId " +
            "{ReorderEventId} on delivery {DeliveryCount} with " +
            "CorrelationId {CorrelationId}.",
            message.MessageId,
            reorderMessage.ReorderEventId,
            message.DeliveryCount,
            correlationId);

        using var scope = _scopeFactory.CreateScope();

        var processor =
            scope.ServiceProvider
                .GetRequiredService<IReorderMessageProcessor>();

        ReorderProcessingResult result;

        try
        {
            result = await processor.ProcessAsync(
                reorderMessage,
                message.MessageId,
                correlationId,
                rawPayload,
                message.DeliveryCount,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled failure while processing message {MessageId} " +
                "with CorrelationId {CorrelationId}.",
                message.MessageId,
                correlationId);

            activity?.SetTag(
                "reorder.outcome",
                "unhandled-failure");

            activity?.SetTag(
                "error.type",
                ex.GetType().FullName);

            activity?.SetStatus(
                ActivityStatusCode.Error,
                ex.Message);

            await SettleFailedMessageAsync(
                receiver,
                message,
                ex.GetBaseException().Message,
                correlationId,
                cancellationToken);

            return;
        }

        switch (result.Outcome)
        {
            case ReorderProcessingOutcome.SupplierAccepted:
                activity?.SetTag(
                    "reorder.outcome",
                    "supplier-accepted");

                activity?.SetTag(
                    "messaging.settlement",
                    "completed");

                activity?.SetStatus(ActivityStatusCode.Ok);

                await receiver.CompleteMessageAsync(
                    message,
                    cancellationToken);

                _logger.LogInformation(
                    "Completed supplier-accepted message " +
                    "{MessageId} with CorrelationId " +
                    "{CorrelationId}.",
                    message.MessageId,
                    correlationId);

                break;

            case ReorderProcessingOutcome.SupplierRejected:
                activity?.SetTag(
                    "reorder.outcome",
                    "supplier-rejected");

                activity?.SetTag(
                    "messaging.settlement",
                    "completed");

                activity?.SetTag(
                    "supplier.rejection.reason",
                    result.Reason);

                // This is a handled terminal business outcome rather
                // than an unhandled technical failure.
                activity?.SetStatus(ActivityStatusCode.Ok);

                await receiver.CompleteMessageAsync(
                    message,
                    cancellationToken);

                _logger.LogWarning(
                    "Completed permanently rejected message " +
                    "{MessageId} with CorrelationId " +
                    "{CorrelationId}. Reason: {Reason}",
                    message.MessageId,
                    correlationId,
                    result.Reason);

                break;

            case ReorderProcessingOutcome.DuplicateSkipped:
                activity?.SetTag(
                    "reorder.outcome",
                    "duplicate-skipped");

                activity?.SetTag(
                    "messaging.settlement",
                    "completed");

                activity?.SetStatus(ActivityStatusCode.Ok);

                await receiver.CompleteMessageAsync(
                    message,
                    cancellationToken);

                _logger.LogInformation(
                    "Completed duplicate message {MessageId} with " +
                    "CorrelationId {CorrelationId} without repeating " +
                    "business processing.",
                    message.MessageId,
                    correlationId);

                break;

            case ReorderProcessingOutcome.Failed:
                activity?.SetTag(
                    "reorder.outcome",
                    "failed");

                activity?.SetStatus(
                    ActivityStatusCode.Error,
                    result.Reason);

                await SettleFailedMessageAsync(
                    receiver,
                    message,
                    result.Reason ?? "Reorder processing failed.",
                    correlationId,
                    cancellationToken);

                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported processing outcome: {result.Outcome}");
        }
    }

    private static string GetCorrelationId(
        ServiceBusReceivedMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            return message.CorrelationId;
        }

        if (message.ApplicationProperties.TryGetValue(
                "CorrelationId",
                out var correlationValue))
        {
            var correlationId = correlationValue?.ToString();

            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static Activity? StartProcessingActivity(
        ServiceBusReceivedMessage message)
    {
        var traceParent =
            GetApplicationProperty(message, "traceparent");

        var traceState =
            GetApplicationProperty(message, "tracestate");

        if (!string.IsNullOrWhiteSpace(traceParent) &&
            ActivityContext.TryParse(
                traceParent,
                traceState,
                isRemote: true,
                out var parentContext))
        {
            return InventoryObservability.ActivitySource
                .StartActivity(
                    "ProcessReorderMessage",
                    ActivityKind.Consumer,
                    parentContext);
        }

        return InventoryObservability.ActivitySource
            .StartActivity(
                "ProcessReorderMessage",
                ActivityKind.Consumer);
    }

    private static string? GetApplicationProperty(
        ServiceBusReceivedMessage message,
        string propertyName)
    {
        if (!message.ApplicationProperties.TryGetValue(
                propertyName,
                out var propertyValue))
        {
            return null;
        }

        return propertyValue?.ToString();
    }

    private async Task SettleFailedMessageAsync(
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        string reason,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var maxDeliveryAttempts =
            Math.Max(1, _options.MaxDeliveryAttempts);

        if (message.DeliveryCount >= maxDeliveryAttempts)
        {
            Activity.Current?.SetTag(
                "messaging.settlement",
                "dead-lettered");

            await receiver.DeadLetterMessageAsync(
                message,
                deadLetterReason: "ReorderProcessingFailed",
                deadLetterErrorDescription: reason,
                cancellationToken);

            _logger.LogWarning(
                "Dead-lettered message {MessageId} with CorrelationId " +
                "{CorrelationId} after {DeliveryCount} delivery attempts. " +
                "Reason: {Reason}",
                message.MessageId,
                correlationId,
                message.DeliveryCount,
                reason);

            return;
        }

        Activity.Current?.SetTag(
            "messaging.settlement",
            "abandoned");

        await receiver.AbandonMessageAsync(
            message,
            cancellationToken: cancellationToken);

        _logger.LogWarning(
            "Abandoned message {MessageId} with CorrelationId " +
            "{CorrelationId} after delivery attempt {DeliveryCount}; " +
            "the message remains retryable. Reason: {Reason}",
            message.MessageId,
            correlationId,
            message.DeliveryCount,
            reason);
    }
}