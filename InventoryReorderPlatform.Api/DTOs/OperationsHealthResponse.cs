namespace InventoryReorderPlatform.Api.DTOs;

public sealed record OperationsHealthResponse(
    string Status,
    string DatabaseStatus,
    int? InventoryItemCount,
    int? ReorderEventCount,
    DateTime CheckedAt
);