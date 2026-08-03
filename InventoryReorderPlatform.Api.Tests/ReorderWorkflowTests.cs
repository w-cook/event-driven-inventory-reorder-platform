extern alias processor;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryReorderPlatform.Api.DTOs;
using InventoryReorderPlatform.Api.Security;
using InventoryReorderPlatform.Api.Services;
using InventoryReorderPlatform.Data;
using InventoryReorderPlatform.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ISupplierOrderClient =
    processor::InventoryReorderPlatform.Processor.Supplier.ISupplierOrderClient;
using ReorderMessageProcessor =
    processor::InventoryReorderPlatform.Processor.Processing.ReorderMessageProcessor;
using ReorderProcessingOutcome =
    processor::InventoryReorderPlatform.Processor.Processing.ReorderProcessingOutcome;
using SupplierOrderRequest =
    processor::InventoryReorderPlatform.Processor.Supplier.SupplierOrderRequest;
using SupplierOrderSubmissionResult =
    processor::InventoryReorderPlatform.Processor.Supplier.SupplierOrderSubmissionResult;

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

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Operator,
                    cancellationToken);

        using var client = authenticated.Client;

        var request = new
        {
            Name = "Low Stock Workflow Item",
            Sku = $"WORKFLOW-{Guid.NewGuid():N}",
            QuantityOnHand = 3,
            ReorderThreshold = 5,
            ReorderQuantity = 12
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
            12,
            createdItem.ReorderQuantity);

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
            12,
            reorderEvent.RequestedQuantity);

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

        Assert.Contains(
            "\"ReorderQuantity\":12",
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
            reorderEvent.RequestedQuantity,
            publishedMessage.RequestedQuantity);

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

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Operator,
                    cancellationToken);

        using var client = authenticated.Client;

        var request = new
        {
            Name = "End-to-End Workflow Item",
            Sku = $"E2E-{Guid.NewGuid():N}",
            QuantityOnHand = 2,
            ReorderThreshold = 5,
            ReorderQuantity = 18
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

        Assert.Equal(
            18,
            publishedMessage.RequestedQuantity);

        using var scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var supplierClient =
            new AcceptingSupplierOrderClient();

        var processor = new ReorderMessageProcessor(
            dbContext,
            supplierClient,
            NullLogger<ReorderMessageProcessor>.Instance);

        var messageId =
            $"reorder-event-{publishedMessage.ReorderEventId}";

        var rawPayload =
            JsonSerializer.Serialize(publishedMessage);

        var processingResult =
            await processor.ProcessAsync(
                publishedMessage,
                messageId,
                "api-workflow-test-correlation",
                rawPayload,
                deliveryCount: 1,
                cancellationToken);

        Assert.Equal(
            ReorderProcessingOutcome.SupplierAccepted,
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
            ReorderEventStatuses.SupplierAccepted,
            reorderEvent.Status);

        Assert.Equal(
            publishedMessage.RequestedQuantity,
            reorderEvent.RequestedQuantity);

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

    [Fact]
    public async Task CreatingItem_WithZeroReorderQuantity_ReturnsBadRequest()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        _factory.MessagePublisher.Clear();

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Operator,
                    cancellationToken);

        using var client = authenticated.Client;

        var request = new
        {
            Name = "Invalid Reorder Quantity Item",
            Sku = $"INVALID-REORDER-{Guid.NewGuid():N}",
            QuantityOnHand = 10,
            ReorderThreshold = 5,
            ReorderQuantity = 0
        };

        var response = await client.PostAsJsonAsync(
            "/api/inventoryitems",
            request,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Empty(
            _factory.MessagePublisher.Messages);
    }

    [Fact]
    public async Task UpdatingConfiguredReorderQuantity_DoesNotChangeExistingRequestedQuantity()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        _factory.MessagePublisher.Clear();

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Operator,
                    cancellationToken);

        using var client = authenticated.Client;

        var createRequest = new
        {
            Name = "Reorder Snapshot Item",
            Sku = $"SNAPSHOT-{Guid.NewGuid():N}",
            QuantityOnHand = 3,
            ReorderThreshold = 5,
            ReorderQuantity = 12
        };

        var createResponse = await client.PostAsJsonAsync(
            "/api/inventoryitems",
            createRequest,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var createdItem =
            await createResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse>(
                    cancellationToken: cancellationToken);

        Assert.NotNull(createdItem);

        var originalMessage =
            Assert.Single(
                _factory.MessagePublisher.Messages);

        Assert.Equal(
            12,
            originalMessage.RequestedQuantity);

        var updateRequest = new
        {
            Name = createdItem.Name,
            Sku = createdItem.Sku,
            QuantityOnHand = createdItem.QuantityOnHand,
            ReorderThreshold = createdItem.ReorderThreshold,
            ReorderQuantity = 24
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/inventoryitems/{createdItem.Id}",
            updateRequest,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            updateResponse.StatusCode);

        var updatedItem =
            await updateResponse.Content
                .ReadFromJsonAsync<InventoryItemResponse>(
                    cancellationToken: cancellationToken);

        Assert.NotNull(updatedItem);

        Assert.Equal(
            24,
            updatedItem.ReorderQuantity);

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
            12,
            reorderEvent.RequestedQuantity);

        Assert.Single(
            _factory.MessagePublisher.Messages);
    }

    [Fact]
    public async Task ReorderEvents_ReturnSupplierSubmissionDetails()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var acceptedSupplierOrderId =
            Guid.NewGuid();

        var acceptedAtUtc = new DateTime(
            2026,
            8,
            3,
            13,
            0,
            0,
            DateTimeKind.Utc);

        int acceptedEventId;
        int rejectedEventId;

        using (var scope =
            _factory.Services.CreateScope())
        {
            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var inventoryItem = new InventoryItem
            {
                Name = "Supplier Visibility Item",
                Sku =
                    $"SUPPLIER-VISIBILITY-" +
                    $"{Guid.NewGuid():N}",
                QuantityOnHand = 2,
                ReorderThreshold = 5,
                ReorderQuantity = 20,
                Status = "ReorderPending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.InventoryItems.Add(inventoryItem);

            await dbContext.SaveChangesAsync(
                cancellationToken);

            var acceptedEventEntity = new ReorderEvent
            {
                InventoryItemId = inventoryItem.Id,
                QuantityAtTrigger = 2,
                RequestedQuantity = 20,
                TriggeredAt = acceptedAtUtc.AddMinutes(-5),
                Status = ReorderEventStatuses.SupplierAccepted,
                SupplierOrderId = acceptedSupplierOrderId,
                SupplierOrderStatus = "Accepted",
                SupplierAcceptedAtUtc = acceptedAtUtc
            };

            var rejectedEventEntity = new ReorderEvent
            {
                InventoryItemId = inventoryItem.Id,
                QuantityAtTrigger = 1,
                RequestedQuantity = 20,
                TriggeredAt = acceptedAtUtc.AddMinutes(-10),
                Status = ReorderEventStatuses.SupplierRejected,
                SupplierOrderStatus = "Rejected",
                SupplierRejectionReason =
                    "The requested SKU is unavailable."
            };

            dbContext.ReorderEvents.AddRange(
                acceptedEventEntity,
                rejectedEventEntity);

            await dbContext.SaveChangesAsync(
                cancellationToken);

            acceptedEventId = acceptedEventEntity.Id;
            rejectedEventId = rejectedEventEntity.Id;
        }

        var authenticated =
            await TestAuthentication
                .CreateAuthenticatedClientAsync(
                    _factory,
                    AppRoles.Viewer,
                    cancellationToken);

        using var client = authenticated.Client;

        var response = await client.GetAsync(
            "/api/reorderevents",
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var events =
            await response.Content
                .ReadFromJsonAsync<
                    List<ReorderEventResponse>>(
                    cancellationToken:
                        cancellationToken);

        Assert.NotNull(events);

        var acceptedEvent = Assert.Single(
            events,
            item => item.Id == acceptedEventId);

        Assert.Equal(
            ReorderEventStatuses.SupplierAccepted,
            acceptedEvent.Status);

        Assert.Equal(
            acceptedSupplierOrderId,
            acceptedEvent.SupplierOrderId);

        Assert.Equal(
            "Accepted",
            acceptedEvent.SupplierOrderStatus);

        Assert.Equal(
            acceptedAtUtc,
            acceptedEvent.SupplierAcceptedAtUtc);

        Assert.Null(
            acceptedEvent.SupplierRejectionReason);

        var rejectedEvent = Assert.Single(
            events,
            item => item.Id == rejectedEventId);

        Assert.Equal(
            ReorderEventStatuses.SupplierRejected,
            rejectedEvent.Status);

        Assert.Null(rejectedEvent.SupplierOrderId);

        Assert.Equal(
            "Rejected",
            rejectedEvent.SupplierOrderStatus);

        Assert.Null(
            rejectedEvent.SupplierAcceptedAtUtc);

        Assert.Equal(
            "The requested SKU is unavailable.",
            rejectedEvent.SupplierRejectionReason);
    }

    private sealed class AcceptingSupplierOrderClient
        : ISupplierOrderClient
    {
        public Task<SupplierOrderSubmissionResult>
            SubmitOrderAsync(
                SupplierOrderRequest request,
                string idempotencyKey,
                string correlationId,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                SupplierOrderSubmissionResult.Accepted(
                    Guid.Parse(
                        "ea49e210-aedd-4eb8-94a8-c266670ef9ec"),
                    "Accepted",
                    new DateTime(
                        2026,
                        8,
                        3,
                        12,
                        0,
                        0,
                        DateTimeKind.Utc)));
        }
    }
}