namespace InventoryReorderPlatform.Api.Security;

public static class AppPolicies
{
    public const string InventoryRead = "InventoryRead";
    public const string InventoryOperate = "InventoryOperate";
    public const string AdminOnly = "AdminOnly";
}