namespace InventoryReorderPlatform.Data.Models;

public static class ReorderEventStatuses
{
    public const string Pending = "Pending";

    // Retained for reorder events completed before supplier
    // submission was introduced in Phase 11.
    public const string Processed = "Processed";

    public const string SupplierAccepted = "SupplierAccepted";

    public const string SupplierRejected = "SupplierRejected";

    public static bool IsTerminal(string status)
    {
        return status is
            Processed or
            SupplierAccepted or
            SupplierRejected;
    }
}