namespace InventoryReorderPlatform.Api.Security;

public sealed record AccessTokenResult(
    string AccessToken,
    DateTime ExpiresAtUtc);