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

    private async Task StoreEventAsync<TPayload>(
        Guid tenantId,
        Guid storeId,
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        await SendBestEffortAsync(
            _hubContext.Clients.Groups(
                RealtimeGroups.PlatformAdmins,
                RealtimeGroups.Store(tenantId, storeId)),
            eventName,
            payload);
    }

    private async Task TenantEventAsync<TPayload>(
        Guid tenantId,
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        await SendBestEffortAsync(
            _hubContext.Clients.Groups(
                RealtimeGroups.PlatformAdmins,
                RealtimeGroups.Tenant(tenantId)),
            eventName,
            payload);
    }

    private static async Task SendBestEffortAsync<TPayload>(
        IClientProxy clientProxy,
        string eventName,
        TPayload payload)
    {
        try
        {
            await clientProxy.SendAsync(eventName, payload, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Realtime fan-out is best-effort and should not fail a committed write.
        }
    }
}
