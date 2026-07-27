using System.Text.Json;
using Azure.Messaging.ServiceBus;
using InventoryReorderPlatform.Contracts.Configuration;
using InventoryReorderPlatform.Contracts.Messages;
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
                "Message {MessageId} contained invalid JSON.",
                message.MessageId);

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
                "Message {MessageId} did not contain a valid " +
                "reorder request.",
                message.MessageId);

            await receiver.DeadLetterMessageAsync(
                message,
                deadLetterReason: "InvalidPayload",
                deadLetterErrorDescription:
                    "The message body did not contain a reorder request.",
                cancellationToken);

            return;
        }

        _logger.LogInformation(
            "Received reorder message {MessageId} for " +
            "ReorderEventId {ReorderEventId} on delivery {DeliveryCount}.",
            message.MessageId,
            reorderMessage.ReorderEventId,
            message.DeliveryCount);

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
                rawPayload,
                message.DeliveryCount,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled failure while processing message {MessageId}.",
                message.MessageId);

            await SettleFailedMessageAsync(
                receiver,
                message,
                ex.GetBaseException().Message,
                cancellationToken);

            return;
        }

        switch (result.Outcome)
        {
            case ReorderProcessingOutcome.Processed:
                await receiver.CompleteMessageAsync(
                    message,
                    cancellationToken);

                _logger.LogInformation(
                    "Completed processed message {MessageId}.",
                    message.MessageId);

                break;

            case ReorderProcessingOutcome.DuplicateSkipped:
                await receiver.CompleteMessageAsync(
                    message,
                    cancellationToken);

                _logger.LogInformation(
                    "Completed duplicate message {MessageId} " +
                    "without repeating business processing.",
                    message.MessageId);

                break;

            case ReorderProcessingOutcome.Failed:
                await SettleFailedMessageAsync(
                    receiver,
                    message,
                    result.Reason ?? "Reorder processing failed.",
                    cancellationToken);

                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported processing outcome: {result.Outcome}");
        }
    }

    private async Task SettleFailedMessageAsync(
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        string reason,
        CancellationToken cancellationToken)
    {
        var maxDeliveryAttempts =
            Math.Max(1, _options.MaxDeliveryAttempts);

        if (message.DeliveryCount >= maxDeliveryAttempts)
        {
            await receiver.DeadLetterMessageAsync(
                message,
                deadLetterReason: "ReorderProcessingFailed",
                deadLetterErrorDescription: reason,
                cancellationToken);

            _logger.LogWarning(
                "Dead-lettered message {MessageId} after " +
                "{DeliveryCount} delivery attempts. Reason: {Reason}",
                message.MessageId,
                message.DeliveryCount,
                reason);

            return;
        }

        await receiver.AbandonMessageAsync(
            message,
            cancellationToken: cancellationToken);

        _logger.LogWarning(
            "Abandoned message {MessageId} after delivery attempt " +
            "{DeliveryCount}; the message remains retryable. " +
            "Reason: {Reason}",
            message.MessageId,
            message.DeliveryCount,
            reason);
    }
}