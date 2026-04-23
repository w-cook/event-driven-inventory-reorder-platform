using System.Text.Json;
using Azure.Messaging.ServiceBus;
using InventoryReorderPlatform.Contracts.Configuration;
using InventoryReorderPlatform.Contracts.Messages;
using Microsoft.Extensions.Options;

namespace InventoryReorderPlatform.Api.Services
{
    public class ReorderMessagePublisher
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusOptions _options;
        private readonly ILogger<ReorderMessagePublisher> _logger;

        public ReorderMessagePublisher(
            ServiceBusClient serviceBusClient,
            IOptions<ServiceBusOptions> options,
            ILogger<ReorderMessagePublisher> logger)
        {
            _serviceBusClient = serviceBusClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task PublishAsync(ReorderRequestedMessage message, CancellationToken cancellationToken = default)
        {
            var sender = _serviceBusClient.CreateSender(_options.QueueName);

            var json = JsonSerializer.Serialize(message);

            var serviceBusMessage = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                Subject = "ReorderRequested"
            };

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            _logger.LogInformation(
                "Published reorder message for ReorderEventId {ReorderEventId}.",
                message.ReorderEventId);
        }
    }
}