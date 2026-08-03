namespace InventoryReorderPlatform.SupplierMockApi.Contracts;

public static class SupplierApiHeaders
{
    public const string IdempotencyKey =
        "Idempotency-Key";

    public const string CorrelationId =
        "X-Correlation-Id";
}