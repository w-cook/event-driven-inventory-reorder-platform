namespace InventoryReorderPlatform.Contracts.Configuration
{
    public class ServiceBusOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string QueueName { get; set; } = string.Empty;
        public int MaxDeliveryAttempts { get; set; } = 3;
    }
}
