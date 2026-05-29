namespace ChainPOS.Services.Realtime;

public interface IRealtimeNotifier
{
    Task InventoryChangedAsync(InventoryChangedEvent payload, CancellationToken cancellationToken = default);

    Task OrderCreatedAsync(OrderCreatedEvent payload, CancellationToken cancellationToken = default);

    Task OrderCancelledAsync(OrderCancelledEvent payload, CancellationToken cancellationToken = default);

    Task ShiftChangedAsync(ShiftChangedEvent payload, CancellationToken cancellationToken = default);

    Task SubscriptionChangedAsync(SubscriptionChangedEvent payload, CancellationToken cancellationToken = default);

    Task SystemPaymentChangedAsync(SystemPaymentChangedEvent payload, CancellationToken cancellationToken = default);
}
