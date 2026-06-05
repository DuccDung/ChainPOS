using ChainPOS.Realtime;
using ChainPOS.Services.Realtime;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace ChainPOS.Tests;

public sealed class RealtimeNotifierTests
{
    [Fact]
    public async Task Store_scoped_events_target_only_the_matching_store_group_and_platform_admins()
    {
        var tenantId = Guid.NewGuid();
        var storeA = Guid.NewGuid();
        var storeB = Guid.NewGuid();
        var clients = new RecordingHubClients();
        var notifier = new SignalRRealtimeNotifier(new RecordingHubContext(clients));

        await notifier.InventoryChangedAsync(new InventoryChangedEvent(
            tenantId,
            storeA,
            Guid.NewGuid(),
            "Store A",
            "A",
            "Keyboard",
            "KEY-001",
            12m,
            2m,
            "Import",
            5m,
            DateTime.UtcNow));
        await notifier.OrderCreatedAsync(new OrderCreatedEvent(
            tenantId,
            storeB,
            Guid.NewGuid(),
            "ORD-001",
            "Store B",
            "B",
            "Cashier",
            2,
            100m,
            "Paid",
            "Completed",
            DateTime.UtcNow));

        Assert.Collection(
            clients.Calls,
            inventory =>
            {
                Assert.Equal("InventoryChanged", inventory.Method);
                Assert.Equal(new[] { RealtimeGroups.PlatformAdmins, RealtimeGroups.Store(tenantId, storeA) }, inventory.Groups);
                Assert.DoesNotContain(RealtimeGroups.Store(tenantId, storeB), inventory.Groups);
            },
            order =>
            {
                Assert.Equal("OrderCreated", order.Method);
                Assert.Equal(new[] { RealtimeGroups.PlatformAdmins, RealtimeGroups.Store(tenantId, storeB) }, order.Groups);
                Assert.DoesNotContain(RealtimeGroups.Store(tenantId, storeA), order.Groups);
            });
    }

    [Fact]
    public async Task Realtime_notifications_are_not_cancelled_by_request_abort_tokens()
    {
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var clients = new RecordingHubClients();
        var notifier = new SignalRRealtimeNotifier(new RecordingHubContext(clients));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await notifier.OrderCancelledAsync(
            new OrderCancelledEvent(
                tenantId,
                storeId,
                Guid.NewGuid(),
                "ORD-CANCEL",
                "Cancelled",
                "Cancelled",
                DateTime.UtcNow),
            cancellation.Token);

        var call = Assert.Single(clients.Calls);
        Assert.Equal("OrderCancelled", call.Method);
        Assert.False(call.CancellationTokenWasCancelled);
    }

    private sealed class RecordingHubContext : IHubContext<ChainPosHub>
    {
        public RecordingHubContext(RecordingHubClients clients)
        {
            Clients = clients;
        }

        public IHubClients Clients { get; }

        public IGroupManager Groups { get; } = new NoopGroupManager();
    }

    private sealed class RecordingHubClients : IHubClients
    {
        public List<RecordedSend> Calls { get; } = new();

        public IClientProxy All => new RecordingClientProxy(this, Array.Empty<string>());

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds)
            => new RecordingClientProxy(this, Array.Empty<string>());

        public IClientProxy Client(string connectionId)
            => new RecordingClientProxy(this, Array.Empty<string>());

        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
            => new RecordingClientProxy(this, Array.Empty<string>());

        public IClientProxy Group(string groupName)
            => new RecordingClientProxy(this, new[] { groupName });

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
            => new RecordingClientProxy(this, new[] { groupName });

        public IClientProxy Groups(IReadOnlyList<string> groupNames)
            => new RecordingClientProxy(this, groupNames.ToArray());

        public IClientProxy User(string userId)
            => new RecordingClientProxy(this, Array.Empty<string>());

        public IClientProxy Users(IReadOnlyList<string> userIds)
            => new RecordingClientProxy(this, Array.Empty<string>());

        public void Add(RecordedSend send) => Calls.Add(send);
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        private readonly RecordingHubClients _clients;
        private readonly IReadOnlyList<string> _groups;

        public RecordingClientProxy(RecordingHubClients clients, IReadOnlyList<string> groups)
        {
            _clients = clients;
            _groups = groups;
        }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            _clients.Add(new RecordedSend(method, _groups.ToArray(), args, cancellationToken.IsCancellationRequested));
            return Task.CompletedTask;
        }
    }

    private sealed class NoopGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed record RecordedSend(
        string Method,
        IReadOnlyList<string> Groups,
        object?[] Args,
        bool CancellationTokenWasCancelled);
}
