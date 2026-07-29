namespace InventoryReorderPlatform.Api.DTOs;

public sealed record AccountResponse(
    string Id,
    string Email,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTime CreatedAtUtc);