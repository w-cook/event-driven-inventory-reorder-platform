using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using InventoryReorderPlatform.Contracts.Configuration;
using InventoryReorderPlatform.Contracts.Messages;
using InventoryReorderPlatform.Observability;
using Microsoft.Extensions.Options;

namespace InventoryReorderPlatform.Api.Services
{
    public class ReorderMessagePublisher
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusOptions _options;
        private readonly ILogger<ReorderMessagePublisher> _logger;
        private readonly ICorrelationIdAccessor _correlationIdAccessor;

        public ReorderMessagePublisher(
            ServiceBusClient serviceBusClient,
            IOptions<ServiceBusOptions> options,
            ILogger<ReorderMessagePublisher> logger,
            ICorrelationIdAccessor correlationIdAccessor)
        {
            _serviceBusClient = serviceBusClient;
            _options = options.Value;
            _logger = logger;
            _correlationIdAccessor = correlationIdAccessor;
        }

        public async Task PublishAsync(
            ReorderRequestedMessage message,
            CancellationToken cancellationToken = default)
        {
            var sender =
                _serviceBusClient.CreateSender(_options.QueueName);

            var json = JsonSerializer.Serialize(message);

            var correlationId =
                _correlationIdAccessor.GetCorrelationId();

            var messageId =
                $"reorder-event-{message.ReorderEventId}";

            using var activity =
                InventoryObservability.ActivitySource.StartActivity(
                    "PublishReorderMessage",
                    ActivityKind.Producer);

            activity?.SetTag(
                "messaging.system",
                "servicebus");

            activity?.SetTag(
                "messaging.destination.name",
                _options.QueueName);

            activity?.SetTag(
                "messaging.message.id",
                messageId);

            activity?.SetTag(
                "messaging.operation.name",
                "publish");

            activity?.SetTag(
                "reorder.event.id",
                message.ReorderEventId);

            activity?.SetTag(
                "inventory.item.id",
                message.InventoryItemId);

            activity?.SetTag(
                "correlation.id",
                correlationId);

            var serviceBusMessage = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                Subject = "ReorderRequested",
                MessageId = messageId,
                CorrelationId = correlationId
            };

            serviceBusMessage.ApplicationProperties["CorrelationId"] =
                correlationId;

            var traceParent =
                activity?.Id ?? Activity.Current?.Id;

            if (!string.IsNullOrWhiteSpace(traceParent))
            {
                serviceBusMessage.ApplicationProperties["traceparent"] =
                    traceParent;
            }

            var traceState =
                activity?.TraceStateString ??
                Activity.Current?.TraceStateString;

            if (!string.IsNullOrWhiteSpace(traceState))
            {
                serviceBusMessage.ApplicationProperties["tracestate"] =
                    traceState;
            }

            try
            {
                await sender.SendMessageAsync(
                    serviceBusMessage,
                    cancellationToken);

                activity?.SetStatus(ActivityStatusCode.Ok);

                _logger.LogInformation(
                    "Published reorder message {MessageId} for " +
                    "ReorderEventId {ReorderEventId} with " +
                    "CorrelationId {CorrelationId}.",
                    serviceBusMessage.MessageId,
                    message.ReorderEventId,
                    correlationId);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(
                    ActivityStatusCode.Error,
                    ex.Message);

                activity?.SetTag(
                    "error.type",
                    ex.GetType().FullName);

                throw;
            }
        }
    }
}