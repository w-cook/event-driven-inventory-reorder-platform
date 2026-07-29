namespace InventoryReorderPlatform.Api.DTOs;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string UserId,
    string Email,
    IReadOnlyList<string> Roles);