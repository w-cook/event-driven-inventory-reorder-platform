namespace InventoryReorderPlatform.Processor.Supplier;

public sealed record SupplierOrderSubmissionResult(
    SupplierOrderSubmissionOutcome Outcome,
    Guid? SupplierOrderId,
    string? SupplierOrderStatus,
    DateTime? AcceptedAtUtc,
    string? RejectionReason)
{
    public static SupplierOrderSubmissionResult Accepted(
        Guid supplierOrderId,
        string supplierOrderStatus,
        DateTime acceptedAtUtc)
    {
        return new SupplierOrderSubmissionResult(
            SupplierOrderSubmissionOutcome.Accepted,
            supplierOrderId,
            supplierOrderStatus,
            acceptedAtUtc,
            RejectionReason: null);
    }

    public static SupplierOrderSubmissionResult Rejected(
        string rejectionReason)
    {
        return new SupplierOrderSubmissionResult(
            SupplierOrderSubmissionOutcome.Rejected,
            SupplierOrderId: null,
            SupplierOrderStatus: "Rejected",
            AcceptedAtUtc: null,
            rejectionReason);
    }
}