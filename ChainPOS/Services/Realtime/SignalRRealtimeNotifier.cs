using ChainPOS.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace ChainPOS.Services.Realtime;

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<ChainPosHub> _hubContext;

    public SignalRRealtimeNotifier(IHubContext<ChainPosHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task InventoryChangedAsync(InventoryChangedEvent payload, CancellationToken cancellationToken = default)
        => StoreEventAsync(payload.TenantId, payload.StoreId, "InventoryChanged", payload, cancellationToken);

    public Task OrderCreatedAsync(OrderCreatedEvent payload, CancellationToken cancellationToken = default)
        => StoreEventAsync(payload.TenantId, payload.StoreId, "OrderCreated", payload, cancellationToken);

    public Task OrderCancelledAsync(OrderCancelledEvent payload, CancellationToken cancellationToken = default)
        => StoreEventAsync(payload.TenantId, payload.StoreId, "OrderCancelled", payload, cancellationToken);

    public Task ShiftChangedAsync(ShiftChangedEvent payload, CancellationToken cancellationToken = default)
        => StoreEventAsync(payload.TenantId, payload.StoreId, "ShiftChanged", payload, cancellationToken);

    public Task SubscriptionChangedAsync(SubscriptionChangedEvent payload, CancellationToken cancellationToken = default)
        => TenantEventAsync(payload.TenantId, "SubscriptionChanged", payload, cancellationToken);

    public Task SystemPaymentChangedAsync(SystemPaymentChangedEvent payload, CancellationToken cancellationToken = default)
        => TenantEventAsync(payload.TenantId, "SystemPaymentChanged", payload, cancellationToken);

    private Task StoreEventAsync<TPayload>(
        Guid tenantId,
        Guid storeId,
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken)
        => _hubContext.Clients
            .Groups(
                RealtimeGroups.PlatformAdmins,
                RealtimeGroups.Tenant(tenantId),
                RealtimeGroups.Store(tenantId, storeId))
            .SendAsync(eventName, payload, cancellationToken);

    private Task TenantEventAsync<TPayload>(
        Guid tenantId,
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken)
        => _hubContext.Clients
            .Groups(
                RealtimeGroups.PlatformAdmins,
                RealtimeGroups.Tenant(tenantId))
            .SendAsync(eventName, payload, cancellationToken);
}
