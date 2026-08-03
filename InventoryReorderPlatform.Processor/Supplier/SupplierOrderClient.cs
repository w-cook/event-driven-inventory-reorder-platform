using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InventoryReorderPlatform.Processor.Supplier;

public sealed class SupplierOrderClient
    : ISupplierOrderClient
{
    private const string SupplierOrdersPath =
        "api/supplier-orders";

    private const string IdempotencyKeyHeader =
        "Idempotency-Key";

    private const string CorrelationIdHeader =
        "X-Correlation-Id";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SupplierOrderClient> _logger;

    public SupplierOrderClient(
        HttpClient httpClient,
        ILogger<SupplierOrderClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SupplierOrderSubmissionResult>
        SubmitOrderAsync(
            SupplierOrderRequest request,
            string idempotencyKey,
            string correlationId,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            correlationId);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            SupplierOrdersPath)
        {
            Content = JsonContent.Create(request)
        };

        httpRequest.Headers.TryAddWithoutValidation(
            IdempotencyKeyHeader,
            idempotencyKey);

        httpRequest.Headers.TryAddWithoutValidation(
            CorrelationIdHeader,
            correlationId);

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode is
            HttpStatusCode.Created or
            HttpStatusCode.OK)
        {
            var supplierOrder =
                await response.Content
                    .ReadFromJsonAsync<SupplierOrderResponse>(
                        cancellationToken);

            if (supplierOrder is null)
            {
                throw new HttpRequestException(
                    "The supplier returned a successful status " +
                    "without a supplier-order response.");
            }

            ValidateAcceptedResponse(
                supplierOrder,
                request,
                idempotencyKey);

            _logger.LogInformation(
                "Supplier accepted order {SupplierOrderId} for " +
                "reorder event {ReorderEventId} with idempotency " +
                "key {IdempotencyKey}.",
                supplierOrder.SupplierOrderId,
                supplierOrder.ReorderEventId,
                idempotencyKey);

            return SupplierOrderSubmissionResult.Accepted(
                supplierOrder.SupplierOrderId,
                supplierOrder.Status,
                supplierOrder.AcceptedAtUtc);
        }

        if (response.StatusCode ==
            HttpStatusCode.UnprocessableEntity)
        {
            var rejectionReason =
                await ReadRejectionReasonAsync(
                    response,
                    cancellationToken);

            _logger.LogWarning(
                "Supplier permanently rejected reorder event " +
                "{ReorderEventId}: {RejectionReason}",
                request.ReorderEventId,
                rejectionReason);

            return SupplierOrderSubmissionResult.Rejected(
                rejectionReason);
        }

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        throw new HttpRequestException(
            $"Supplier submission returned HTTP " +
            $"{(int)response.StatusCode} " +
            $"({response.ReasonPhrase}). " +
            $"Response: {responseBody}",
            inner: null,
            statusCode: response.StatusCode);
    }

    private static void ValidateAcceptedResponse(
        SupplierOrderResponse response,
        SupplierOrderRequest request,
        string idempotencyKey)
    {
        if (response.SupplierOrderId == Guid.Empty)
        {
            throw new HttpRequestException(
                "The supplier accepted the order without " +
                "returning a supplier order identifier.");
        }

        if (!string.Equals(
                response.IdempotencyKey,
                idempotencyKey,
                StringComparison.Ordinal))
        {
            throw new HttpRequestException(
                "The supplier returned an unexpected " +
                "idempotency key.");
        }

        if (response.ReorderEventId !=
                request.ReorderEventId ||
            response.InventoryItemId !=
                request.InventoryItemId ||
            response.RequestedQuantity !=
                request.RequestedQuantity)
        {
            throw new HttpRequestException(
                "The supplier response did not match the " +
                "submitted reorder request.");
        }

        if (string.IsNullOrWhiteSpace(response.Status))
        {
            throw new HttpRequestException(
                "The supplier accepted the order without " +
                "returning a status.");
        }

        if (response.AcceptedAtUtc == default)
        {
            throw new HttpRequestException(
                "The supplier accepted the order without " +
                "returning an acceptance timestamp.");
        }
    }

    private static async Task<string>
        ReadRejectionReasonAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
    {
        try
        {
            var problemDetails =
                await response.Content
                    .ReadFromJsonAsync<SupplierProblemDetails>(
                        cancellationToken);

            if (!string.IsNullOrWhiteSpace(
                    problemDetails?.Detail))
            {
                return problemDetails.Detail;
            }

            if (!string.IsNullOrWhiteSpace(
                    problemDetails?.Title))
            {
                return problemDetails.Title;
            }
        }
        catch (JsonException)
        {
            // Fall back to a stable rejection description.
        }

        return "The supplier permanently rejected the order.";
    }

    private sealed class SupplierProblemDetails
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}