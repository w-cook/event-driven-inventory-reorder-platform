using System.Diagnostics;

namespace InventoryReorderPlatform.Observability;

public static class InventoryObservability
{
    public const string ActivitySourceName =
        "InventoryReorderPlatform.Workflow";

    public static readonly ActivitySource ActivitySource =
        new(ActivitySourceName);
}