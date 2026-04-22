namespace InventoryReorderPlatform.Contracts.Configuration
{
    public class ServiceBusOptions
    {
        public string FullyQualifiedNamespace { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty;
    }
}
