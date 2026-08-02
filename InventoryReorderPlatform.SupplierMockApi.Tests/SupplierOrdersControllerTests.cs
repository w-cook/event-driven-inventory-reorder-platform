using System.Net;
using System.Net.Http.Json;
using InventoryReorderPlatform.SupplierMockApi.Contracts;
using InventoryReorderPlatform.SupplierMockApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryReorderPlatform.SupplierMockApi.Tests;

public sealed class SupplierOrdersControllerTests
    : IClassFixture<SupplierApiFactory>
{
    private readonly SupplierApiFactory _factory;
    private readonly HttpClient _client;

    public SupplierOrdersControllerTests(
        SupplierApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var idempotencyKey = CreateIdempotencyKey();
        var request = CreateValidRequest();

        using var message =
            CreatePostMessage(idempotencyKey, request);

        var response = await _client.SendAsync(
            message,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<SupplierOrderResponse>(
                cancellationToken);

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.SupplierOrderId);
        Assert.Equal(
            idempotencyKey,
            result.IdempotencyKey);
        Assert.Equal(
            request.ReorderEventId,
            result.ReorderEventId);
        Assert.Equal(
            request.InventoryItemId,
            result.InventoryItemId);
        Assert.Equal(request.Sku, result.Sku);
        Assert.Equal(
            request.RequestedQuantity,
            result.RequestedQuantity);
        Assert.Equal("Accepted", result.Status);

        using var scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<SupplierDbContext>();

        var persistedOrder =
            await dbContext.SupplierOrders
                .SingleAsync(
                    order =>
                        order.IdempotencyKey
                        == idempotencyKey,
                    cancellationToken);

        Assert.Equal(
            result.SupplierOrderId,
            persistedOrder.Id);
    }

    [Fact]
    public async Task Create_WithDuplicateRequest_ReturnsOriginalOrder()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var idempotencyKey = CreateIdempotencyKey();
        var request = CreateValidRequest();

        using var firstMessage =
            CreatePostMessage(idempotencyKey, request);

        var firstResponse =
            await _client.SendAsync(
                firstMessage,
                cancellationToken);

        var firstResult =
            await firstResponse.Content
                .ReadFromJsonAsync<SupplierOrderResponse>(
                    cancellationToken);

        using var secondMessage =
            CreatePostMessage(idempotencyKey, request);

        var secondResponse =
            await _client.SendAsync(
                secondMessage,
                cancellationToken);

        var secondResult =
            await secondResponse.Content
                .ReadFromJsonAsync<SupplierOrderResponse>(
                    cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            secondResponse.StatusCode);

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);

        Assert.Equal(
            firstResult.SupplierOrderId,
            secondResult.SupplierOrderId);

        Assert.Equal(
            firstResult.AcceptedAtUtc,
            secondResult.AcceptedAtUtc);

        using var scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<SupplierDbContext>();

        var matchingOrderCount =
            await dbContext.SupplierOrders
                .CountAsync(
                    order =>
                        order.IdempotencyKey
                        == idempotencyKey,
                    cancellationToken);

        Assert.Equal(1, matchingOrderCount);
    }

    [Fact]
    public async Task Create_WithConflictingPayload_ReturnsConflict()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var idempotencyKey = CreateIdempotencyKey();
        var originalRequest = CreateValidRequest();

        using var firstMessage =
            CreatePostMessage(
                idempotencyKey,
                originalRequest);

        var firstResponse =
            await _client.SendAsync(
                firstMessage,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var conflictingRequest = CreateValidRequest();
        conflictingRequest.RequestedQuantity = 99;

        using var conflictingMessage =
            CreatePostMessage(
                idempotencyKey,
                conflictingRequest);

        var conflictingResponse =
            await _client.SendAsync(
                conflictingMessage,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.Conflict,
            conflictingResponse.StatusCode);

        using var scope =
            _factory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<SupplierDbContext>();

        var matchingOrderCount =
            await dbContext.SupplierOrders
                .CountAsync(
                    order =>
                        order.IdempotencyKey
                        == idempotencyKey,
                    cancellationToken);

        Assert.Equal(1, matchingOrderCount);
    }

    [Fact]
    public async Task Create_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var request = CreateValidRequest();

        var response =
            await _client.PostAsJsonAsync(
                "/api/supplier-orders",
                request,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidQuantity_ReturnsBadRequest()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var request = CreateValidRequest();
        request.RequestedQuantity = 0;

        using var message =
            CreatePostMessage(
                CreateIdempotencyKey(),
                request);

        var response =
            await _client.SendAsync(
                message,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private static HttpRequestMessage CreatePostMessage(
        string idempotencyKey,
        CreateSupplierOrderRequest request)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/supplier-orders");

        message.Headers.Add(
            SupplierApiHeaders.IdempotencyKey,
            idempotencyKey);

        message.Content = JsonContent.Create(request);

        return message;
    }

    private static CreateSupplierOrderRequest
        CreateValidRequest()
    {
        return new CreateSupplierOrderRequest
        {
            ReorderEventId = 1001,
            InventoryItemId = 25,
            Sku = "WIDGET-100",
            RequestedQuantity = 40,
            TriggeredAtUtc = new DateTime(
                2026,
                8,
                2,
                13,
                30,
                0,
                DateTimeKind.Utc)
        };
    }

    private static string CreateIdempotencyKey()
    {
        return $"supplier-test-{Guid.NewGuid():N}";
    }
}