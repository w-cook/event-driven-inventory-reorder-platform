using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryReorderPlatform.Processor.Supplier;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryReorderPlatform.Processor.Tests;

public sealed class SupplierOrderClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.Created)]
    [InlineData(HttpStatusCode.OK)]
    public async Task SubmitOrderAsync_WhenSupplierAcceptsOrReplays_ReturnsAccepted(
        HttpStatusCode responseStatusCode)
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var supplierOrderId =
            Guid.Parse(
                "65df48cb-29b8-40bb-aae6-2820a3325459");

        var acceptedAtUtc = new DateTime(
            2026,
            8,
            3,
            12,
            30,
            0,
            DateTimeKind.Utc);

        const string idempotencyKey =
            "reorder-event-123";

        const string correlationId =
            "supplier-client-test-correlation";

        var request = CreateRequest();

        var handler = new RecordingHandler(
            (_, _) => Task.FromResult(
                CreateAcceptedResponse(
                    responseStatusCode,
                    supplierOrderId,
                    idempotencyKey,
                    request,
                    acceptedAtUtc)));

        using var httpClient = CreateHttpClient(handler);

        var client = new SupplierOrderClient(
            httpClient,
            NullLogger<SupplierOrderClient>.Instance);

        var result = await client.SubmitOrderAsync(
            request,
            idempotencyKey,
            correlationId,
            cancellationToken);

        Assert.Equal(
            SupplierOrderSubmissionOutcome.Accepted,
            result.Outcome);

        Assert.Equal(
            supplierOrderId,
            result.SupplierOrderId);

        Assert.Equal(
            "Accepted",
            result.SupplierOrderStatus);

        Assert.Equal(
            acceptedAtUtc,
            result.AcceptedAtUtc);

        Assert.Null(result.RejectionReason);

        Assert.Equal(
            HttpMethod.Post,
            handler.Method);

        Assert.Equal(
            "/api/supplier-orders",
            handler.RequestUri?.AbsolutePath);

        Assert.Equal(
            idempotencyKey,
            handler.IdempotencyKey);

        Assert.Equal(
            correlationId,
            handler.CorrelationId);

        Assert.NotNull(handler.Body);

        var submittedRequest =
            JsonSerializer.Deserialize<SupplierOrderRequest>(
                handler.Body,
                new JsonSerializerOptions(
                    JsonSerializerDefaults.Web));

        Assert.NotNull(submittedRequest);

        Assert.Equal(
            request.ReorderEventId,
            submittedRequest.ReorderEventId);

        Assert.Equal(
            request.InventoryItemId,
            submittedRequest.InventoryItemId);

        Assert.Equal(
            request.Sku,
            submittedRequest.Sku);

        Assert.Equal(
            request.RequestedQuantity,
            submittedRequest.RequestedQuantity);

        Assert.Equal(
            request.TriggeredAtUtc,
            submittedRequest.TriggeredAtUtc);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenResponseIsDelayed_WaitsForResponse()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var requestStarted =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        var releaseResponse =
            new TaskCompletionSource<bool>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        const string idempotencyKey =
            "reorder-event-delayed";

        var request = CreateRequest();

        var supplierOrderId = Guid.Parse(
            "dcf191e4-cab2-41ed-b5f8-0d23eb900908");

        var acceptedAtUtc = new DateTime(
            2026,
            8,
            3,
            12,
            45,
            0,
            DateTimeKind.Utc);

        var handler = new RecordingHandler(
            async (_, token) =>
            {
                requestStarted.TrySetResult(true);

                await releaseResponse.Task.WaitAsync(token);

                return CreateAcceptedResponse(
                    HttpStatusCode.Created,
                    supplierOrderId,
                    idempotencyKey,
                    request,
                    acceptedAtUtc);
            });

        using var httpClient = CreateHttpClient(handler);

        var client = new SupplierOrderClient(
            httpClient,
            NullLogger<SupplierOrderClient>.Instance);

        var submissionTask = client.SubmitOrderAsync(
            request,
            idempotencyKey,
            "delayed-test-correlation",
            cancellationToken);

        await requestStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(2),
            cancellationToken);

        try
        {
            Assert.False(submissionTask.IsCompleted);
        }
        finally
        {
            releaseResponse.TrySetResult(true);
        }

        var result = await submissionTask;

        Assert.Equal(
            SupplierOrderSubmissionOutcome.Accepted,
            result.Outcome);

        Assert.Equal(
            supplierOrderId,
            result.SupplierOrderId);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenSupplierRejects_ReturnsRejected()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        const string rejectionReason =
            "The requested SKU is unavailable.";

        var handler = new RecordingHandler(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.UnprocessableEntity)
                {
                    Content = JsonContent.Create(new
                    {
                        title = "Supplier order rejected",
                        detail = rejectionReason
                    })
                }));

        using var httpClient = CreateHttpClient(handler);

        var client = new SupplierOrderClient(
            httpClient,
            NullLogger<SupplierOrderClient>.Instance);

        var result = await client.SubmitOrderAsync(
            CreateRequest(),
            "reorder-event-rejected",
            "rejected-test-correlation",
            cancellationToken);

        Assert.Equal(
            SupplierOrderSubmissionOutcome.Rejected,
            result.Outcome);

        Assert.Null(result.SupplierOrderId);

        Assert.Equal(
            "Rejected",
            result.SupplierOrderStatus);

        Assert.Null(result.AcceptedAtUtc);

        Assert.Equal(
            rejectionReason,
            result.RejectionReason);
    }

    [Fact]
    public async Task SubmitOrderAsync_WhenSupplierIsUnavailable_ThrowsRetryableException()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        var handler = new RecordingHandler(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable)
                {
                    Content = JsonContent.Create(new
                    {
                        title = "Transient supplier failure",
                        detail =
                            "The supplier is temporarily unavailable."
                    })
                }));

        using var httpClient = CreateHttpClient(handler);

        var client = new SupplierOrderClient(
            httpClient,
            NullLogger<SupplierOrderClient>.Instance);

        var exception =
            await Assert.ThrowsAsync<HttpRequestException>(
                () => client.SubmitOrderAsync(
                    CreateRequest(),
                    "reorder-event-transient",
                    "transient-test-correlation",
                    cancellationToken));

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            exception.StatusCode);
    }

    private static HttpClient CreateHttpClient(
        HttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress =
                new Uri("http://supplier.test/")
        };
    }

    private static SupplierOrderRequest CreateRequest()
    {
        return new SupplierOrderRequest
        {
            ReorderEventId = 123,
            InventoryItemId = 456,
            Sku = "CLIENT-TEST-001",
            RequestedQuantity = 25,
            TriggeredAtUtc = new DateTime(
                2026,
                8,
                3,
                12,
                0,
                0,
                DateTimeKind.Utc)
        };
    }

    private static HttpResponseMessage
        CreateAcceptedResponse(
            HttpStatusCode statusCode,
            Guid supplierOrderId,
            string idempotencyKey,
            SupplierOrderRequest request,
            DateTime acceptedAtUtc)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(
                new SupplierOrderResponse
                {
                    SupplierOrderId = supplierOrderId,
                    IdempotencyKey = idempotencyKey,
                    ReorderEventId =
                        request.ReorderEventId,
                    InventoryItemId =
                        request.InventoryItemId,
                    Sku = request.Sku,
                    RequestedQuantity =
                        request.RequestedQuantity,
                    TriggeredAtUtc =
                        request.TriggeredAtUtc,
                    Status = "Accepted",
                    AcceptedAtUtc = acceptedAtUtc
                })
        };
    }

    private sealed class RecordingHandler
        : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> _responseFactory;

        public RecordingHandler(
            Func<
                HttpRequestMessage,
                CancellationToken,
                Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? IdempotencyKey { get; private set; }

        public string? CorrelationId { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage>
            SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;

            IdempotencyKey =
                request.Headers.TryGetValues(
                    "Idempotency-Key",
                    out var idempotencyValues)
                    ? idempotencyValues.Single()
                    : null;

            CorrelationId =
                request.Headers.TryGetValues(
                    "X-Correlation-Id",
                    out var correlationValues)
                    ? correlationValues.Single()
                    : null;

            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(
                    cancellationToken);

            return await _responseFactory(
                request,
                cancellationToken);
        }
    }
}