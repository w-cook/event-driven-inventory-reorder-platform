using InventoryReorderPlatform.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryReorderPlatform.Processor;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;

    public Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reorder event processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var pendingEvents = await dbContext.ReorderEvents
                    .Where(e => e.Status == "Pending")
                    .OrderBy(e => e.TriggeredAt)
                    .Take(5)
                    .ToListAsync(stoppingToken);

                if (pendingEvents.Count == 0)
                {
                    _logger.LogInformation("No pending reorder events found.");
                }
                else
                {
                    _logger.LogInformation("Found {Count} pending reorder event(s).", pendingEvents.Count);

                    foreach (var reorderEvent in pendingEvents)
                    {
                        reorderEvent.Status = "Processed";

                        _logger.LogInformation(
                            "Processed reorder event {EventId} for inventory item {InventoryItemId}.",
                            reorderEvent.Id,
                            reorderEvent.InventoryItemId);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing reorder events.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}