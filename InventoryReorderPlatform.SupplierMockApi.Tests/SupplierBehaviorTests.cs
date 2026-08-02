using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using InventoryReorderPlatform.SupplierMockApi.Behavior;
using InventoryReorderPlatform.SupplierMockApi.Contracts;
using InventoryReorderPlatform.SupplierMockApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryReorderPlatform.SupplierMockApi.Tests;

public sealed class SupplierBehaviorTests
    : IClassFixture<SupplierApiFactory>
{
    private readonly SupplierApiFactory _factory;

    public SupplierBehaviorTests(
        SupplierApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_InDelayedMode_WaitsBeforeAcceptance()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var configuredFactory =
            CreateConfiguredFactory(
                SupplierBehaviorMode.Delayed,
                delayMilliseconds: 150);

        using var client =
            configuredFactory.CreateClient();

        var idempotencyKey = CreateIdempotencyKey();
        var request = CreateValidRequest();

        var stopwatch = Stopwatch.StartNew();

        using var response = await SendAsync(
            client,
            idempotencyKey,
            request,
            cancellationToken);

        stopwatch.Stop();

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        Assert.True(
            stopwatch.Elapsed >=
                TimeSpan.FromMilliseconds(100),
            $"Expected a simulated delay, but the request " +
            $"completed in {stopwatch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task Create_InTransientFailureMode_Recovers()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var configuredFactory =
            CreateConfiguredFactory(
                SupplierBehaviorMode.TransientFailure,
                transientFailuresBeforeSuccess: 2);

        using var client =
            configuredFactory.CreateClient();

        var idempotencyKey = CreateIdempotencyKey();
        var request = CreateValidRequest();

        using var firstResponse = await SendAsync(
            client,
            idempotencyKey,
            request,
            cancellationToken);

        using var secondResponse = await SendAsync(
            client,
            idempotencyKey,
            request,
            cancellationToken);

        using var thirdResponse = await SendAsync(
            client,
            idempotencyKey,
            request,
            cancellationToken);

        using var fourthResponse = await SendAsync(
            client,
            idempotencyKey,
            request,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            firstResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            secondResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Created,
            thirdResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            fourthResponse.StatusCode);

        Assert.Equal(
            "1",
            firstResponse.Headers
                .GetValues("Retry-After")
                .Single());

        var acceptedOrder = await thirdResponse.Content
            .ReadFromJsonAsync<SupplierOrderResponse>(
                cancellationToken);

        var replayedOrder = await fourthResponse.Content
            .ReadFromJsonAsync<SupplierOrderResponse>(
                cancellationToken);

        Assert.NotNull(acceptedOrder);
        Assert.NotNull(replayedOrder);

        Assert.Equal(
            acceptedOrder.SupplierOrderId,
            replayedOrder.SupplierOrderId);

        Assert.Equal(
            acceptedOrder.AcceptedAtUtc,
            replayedOrder.AcceptedAtUtc);

        using var scope =
            configuredFactory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<SupplierDbContext>();

        var orderCount =
            await dbContext.SupplierOrders.CountAsync(
                order =>
                    order.IdempotencyKey
                    == idempotencyKey,
                cancellationToken);

        Assert.Equal(1, orderCount);
    }

    [Fact]
    public async Task Create_InPermanentRejectionMode_ReturnsUnprocessableEntity()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        const string rejectionMessage =
            "The requested SKU is unavailable.";

        using var configuredFactory =
            CreateConfiguredFactory(
                SupplierBehaviorMode.PermanentRejection,
                permanentRejectionMessage:
                    rejectionMessage);

        using var client =
            configuredFactory.CreateClient();

        var idempotencyKey = CreateIdempotencyKey();
        var request = CreateValidRequest();

        using var response = await SendAsync(
            client,
            idempotencyKey,
            request,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            response.StatusCode);

        var problem = await response.Content
            .ReadFromJsonAsync<ProblemDetails>(
                cancellationToken);

        Assert.NotNull(problem);
        Assert.Equal(
            "Supplier order rejected",
            problem.Title);
        Assert.Equal(
            rejectionMessage,
            problem.Detail);

        using var scope =
            configuredFactory.Services.CreateScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<SupplierDbContext>();

        var orderCount =
            await dbContext.SupplierOrders.CountAsync(
                order =>
                    order.IdempotencyKey
                    == idempotencyKey,
                cancellationToken);

        Assert.Equal(0, orderCount);
    }

    [Fact]
    public async Task Create_ExistingAcceptedOrder_BypassesLaterFailureMode()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var idempotencyKey = CreateIdempotencyKey();
        var request = CreateValidRequest();

        using var normalClient = _factory.CreateClient();

        using var firstResponse = await SendAsync(
            normalClient,
            idempotencyKey,
            request,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        var firstResult = await firstResponse.Content
            .ReadFromJsonAsync<SupplierOrderResponse>(
                cancellationToken);

        using var rejectionFactory =
            CreateConfiguredFactory(
                SupplierBehaviorMode.PermanentRejection);

        using var rejectionClient =
            rejectionFactory.CreateClient();

        using var replayResponse = await SendAsync(
            rejectionClient,
            idempotencyKey,
            request,
            cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            replayResponse.StatusCode);

        var replayResult = await replayResponse.Content
            .ReadFromJsonAsync<SupplierOrderResponse>(
                cancellationToken);

        Assert.NotNull(firstResult);
        Assert.NotNull(replayResult);

        Assert.Equal(
            firstResult.SupplierOrderId,
            replayResult.SupplierOrderId);

        Assert.Equal(
            firstResult.AcceptedAtUtc,
            replayResult.AcceptedAtUtc);
    }

    private WebApplicationFactory<Program>
        CreateConfiguredFactory(
            SupplierBehaviorMode mode,
            int delayMilliseconds = 0,
            int transientFailuresBeforeSuccess = 2,
            string permanentRejectionMessage =
                "The supplier rejected the requested order.")
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            [
                                $"{SupplierBehaviorOptions.SectionName}:Mode"
                            ] = mode.ToString(),

                            [
                                $"{SupplierBehaviorOptions.SectionName}:DelayMilliseconds"
                            ] = delayMilliseconds.ToString(),

                            [
                                $"{SupplierBehaviorOptions.SectionName}:TransientFailuresBeforeSuccess"
                            ] = transientFailuresBeforeSuccess
                                .ToString(),

                            [
                                $"{SupplierBehaviorOptions.SectionName}:PermanentRejectionMessage"
                            ] = permanentRejectionMessage
                        });
                });
        });
    }

    private static async Task<HttpResponseMessage>
        SendAsync(
            HttpClient client,
            string idempotencyKey,
            CreateSupplierOrderRequest request,
            CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/supplier-orders");

        message.Headers.Add(
            SupplierApiHeaders.IdempotencyKey,
            idempotencyKey);

        message.Content = JsonContent.Create(request);

        return await client.SendAsync(
            message,
            cancellationToken);
    }

    private static CreateSupplierOrderRequest
        CreateValidRequest()
    {
        return new CreateSupplierOrderRequest
        {
            ReorderEventId = 3001,
            InventoryItemId = 35,
            Sku = "BEHAVIOR-TEST-100",
            RequestedQuantity = 50,
            TriggeredAtUtc = new DateTime(
                2026,
                8,
                2,
                14,
                0,
                0,
                DateTimeKind.Utc)
        };
    }

    private static string CreateIdempotencyKey()
    {
        return $"behavior-test-{Guid.NewGuid():N}";
    }
}