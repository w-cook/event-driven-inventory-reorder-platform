namespace InventoryReorderPlatform.Contracts.Messages
{
    public class ReorderRequestedMessage
    {
        public int ReorderEventId { get; set; }
        public int InventoryItemId { get; set; }
        public string Sku { get; set; } = string.Empty;
        public int QuantityAtTrigger { get; set; }
        public DateTime TriggeredAt { get; set; }
    }
}
