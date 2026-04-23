using System.Text.Json;
using Azure.Messaging.ServiceBus;
using InventoryReorderPlatform.Contracts.Configuration;
using InventoryReorderPlatform.Contracts.Messages;
using InventoryReorderPlatform.Data;
using Microsoft.EntityFrameworkCore;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Service Bus processor worker started.");

        await using var receiver = _serviceBusClient.CreateReceiver(_options.QueueName);

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
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }

                foreach (var message in messages)
                {
                    var body = message.Body.ToString();

                    var reorderMessage = JsonSerializer.Deserialize<ReorderRequestedMessage>(body);

                    if (reorderMessage == null)
                    {
                        _logger.LogWarning("Received invalid reorder message.");
                        await receiver.CompleteMessageAsync(message, stoppingToken);
                        continue;
                    }

                    _logger.LogInformation(
                        "Received reorder message for ReorderEventId {ReorderEventId}.",
                        reorderMessage.ReorderEventId);

                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var reorderEvent = await dbContext.ReorderEvents
                        .FirstOrDefaultAsync(
                            r => r.Id == reorderMessage.ReorderEventId,
                            stoppingToken);

                    if (reorderEvent == null)
                    {
                        _logger.LogWarning(
                            "Reorder event {ReorderEventId} was not found.",
                            reorderMessage.ReorderEventId);

                        await receiver.CompleteMessageAsync(message, stoppingToken);
                        continue;
                    }

                    if (reorderEvent.Status == "Processed")
                    {
                        _logger.LogInformation(
                            "Reorder event {ReorderEventId} was already processed.",
                            reorderEvent.Id);

                        await receiver.CompleteMessageAsync(message, stoppingToken);
                        continue;
                    }

                    reorderEvent.Status = "Processed";
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation(
                        "Processed reorder event {ReorderEventId} for item {InventoryItemId}.",
                        reorderEvent.Id,
                        reorderEvent.InventoryItemId);

                    await receiver.CompleteMessageAsync(message, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing Service Bus messages.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}