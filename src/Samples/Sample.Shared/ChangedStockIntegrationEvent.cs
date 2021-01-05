using EventBus.Events;

namespace Sample.Shared
{
    public class ChangedStockIntegrationEvent : IntegrationEvent
    {
        public int ProductId { get; }
        public int Quantity { get; }
        public ChangedStockIntegrationEvent(int productId, int quantity)
        {
            ProductId = productId;
            Quantity = quantity;
        }
    }
}
