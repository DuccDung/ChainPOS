using ChainPOS.Services.Realtime;

namespace ChainPOS.Tests.TestSupport;

internal sealed class FakeRealtimeNotifier : IRealtimeNotifier
{
    public List<string> Events { get; } = new();

    public Task InventoryChangedAsync(InventoryChangedEvent payload, CancellationToken cancellationToken = default)
    {
        Events.Add(nameof(InventoryChangedAsync));
        return Task.CompletedTask;
    }

    public Task OrderCreatedAsync(OrderCreatedEvent payload, CancellationToken cancellationToken = default)
    {
        Events.Add(nameof(OrderCreatedAsync));
        return Task.CompletedTask;
    }

    public Task OrderCancelledAsync(OrderCancelledEvent payload, CancellationToken cancellationToken = default)
    {
        Events.Add(nameof(OrderCancelledAsync));
        return Task.CompletedTask;
    }

    public Task ShiftChangedAsync(ShiftChangedEvent payload, CancellationToken cancellationToken = default)
    {
        Events.Add(nameof(ShiftChangedAsync));
        return Task.CompletedTask;
    }

    public Task SubscriptionChangedAsync(SubscriptionChangedEvent payload, CancellationToken cancellationToken = default)
    {
        Events.Add(nameof(SubscriptionChangedAsync));
        return Task.CompletedTask;
    }

    public Task SystemPaymentChangedAsync(SystemPaymentChangedEvent payload, CancellationToken cancellationToken = default)
    {
        Events.Add(nameof(SystemPaymentChangedAsync));
        return Task.CompletedTask;
    }
}
