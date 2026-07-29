extern alias processor;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReorderMessageProcessor =
    processor::InventoryReorderPlatform.Processor.Processing.ReorderMessageProcessor;
using ReorderProcessingOutcome =
    processor::InventoryReorderPlatform.Processor.Processing.ReorderProcessingOutcome;

namespace InventoryReorderPlatform.Api.Tests;

public sealed class ReorderWorkflowTests
    : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public ReorderWorkflowTests(
        InventoryApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreatingLowStockItem_CreatesAndPublishesReorderEvent()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        _factory.MessagePublisher.Clear();

        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Demo-User",
            "operator");

        var request = new
        {
            Name = "Low Stock Workflow Item",
            Sku = $"WORKFLOW-{Guid.NewGuid():N}",
            QuantityOnHand = 3,
            ReorderThreshold = 5
        };

        var response = await client.PostAsJsonAsync(
            "/api/inventoryitems",
            request,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var createdItem =
            await response.Content
                .ReadFromJsonAsync<InventoryItemResponse>(
                    cancellationToken: cancellationToken);

        Assert.NotNull(createdItem);

        Assert.Equal(
            "ReorderPending",
            createdItem.Status);

        using var scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var reorderEvent =
            await dbContext.ReorderEvents
                .AsNoTracking()
                .SingleAsync(
                    item =>
                        item.InventoryItemId ==
                        createdItem.Id,
                    cancellationToken);

        Assert.Equal(
            3,
            reorderEvent.QuantityAtTrigger);

        Assert.Equal(
            "Pending",
            reorderEvent.Status);

        var historyEntry =
            await dbContext.ReorderHistoryEntries
                .AsNoTracking()
                .SingleAsync(
                    item =>
                        item.InventoryItemId ==
                        createdItem.Id,
                    cancellationToken);

        Assert.Equal(
            "Active",
            historyEntry.OldStatus);

        Assert.Equal(
            "ReorderPending",
            historyEntry.NewStatus);

        var auditRecord =
            await dbContext.AuditRecords
                .AsNoTracking()
                .SingleAsync(
                    item =>
                        item.Action ==
                            AuditActions.InventoryItemCreated
                        && item.EntityId ==
                            createdItem.Id.ToString(),
                    cancellationToken);

        Assert.Contains(
            "\"ReorderEventCreated\":true",
            auditRecord.Details);

        var publishedMessage =
            Assert.Single(
                _factory.MessagePublisher.Messages);

        Assert.Equal(
            reorderEvent.Id,
            publishedMessage.ReorderEventId);

        Assert.Equal(
            createdItem.Id,
            publishedMessage.InventoryItemId);

        Assert.Equal(
            createdItem.Sku,
            publishedMessage.Sku);

        Assert.Equal(
            reorderEvent.QuantityAtTrigger,
            publishedMessage.QuantityAtTrigger);

        Assert.Equal(
            reorderEvent.TriggeredAt,
            publishedMessage.TriggeredAt);
    }

    [Fact]
    public async Task LowStockWorkflow_IsProcessedEndToEnd()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        _factory.MessagePublisher.Clear();

        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Demo-User",
            "operator");

        var request = new
        {
            Name = "End-to-End Workflow Item",
            Sku = $"E2E-{Guid.NewGuid():N}",
            QuantityOnHand = 2,
            ReorderThreshold = 5
        };

        var response = await client.PostAsJsonAsync(
            "/api/inventoryitems",
            request,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var createdItem =
            await response.Content
                .ReadFromJsonAsync<InventoryItemResponse>(
                    cancellationToken: cancellationToken);

        Assert.NotNull(createdItem);

        var publishedMessage =
            Assert.Single(
                _factory.MessagePublisher.Messages);

        using var scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var processor = new ReorderMessageProcessor(
            dbContext,
            NullLogger<ReorderMessageProcessor>.Instance);

        var messageId =
            $"reorder-event-{publishedMessage.ReorderEventId}";

        var rawPayload =
            JsonSerializer.Serialize(publishedMessage);

        var processingResult =
            await processor.ProcessAsync(
                publishedMessage,
                messageId,
                rawPayload,
                deliveryCount: 1,
                cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.Processed,
            processingResult.Outcome);

        var reorderEvent =
            await dbContext.ReorderEvents
                .AsNoTracking()
                .SingleAsync(
                    item =>
                        item.Id ==
                        publishedMessage.ReorderEventId,
                    cancellationToken);

        Assert.Equal(
            createdItem.Id,
            reorderEvent.InventoryItemId);

        Assert.Equal(
            "Processed",
            reorderEvent.Status);

        var processedMessage =
            await dbContext.ProcessedMessages
                .AsNoTracking()
                .SingleAsync(
                    item =>
                        item.MessageId == messageId,
                    cancellationToken);

        Assert.Equal(
            "ReorderRequestedMessage",
            processedMessage.MessageType);

        Assert.NotEqual(
            default,
            processedMessage.ProcessedAtUtc);

        Assert.False(
            await dbContext.FailedMessages
                .AnyAsync(
                    item =>
                        item.MessageId == messageId,
                    cancellationToken));
    }
}