using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Realtime;
using ChainPOS.Tests.TestSupport;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace ChainPOS.Tests;

public sealed class RealtimeHubTests
{
    [Fact]
    public async Task Locked_user_connection_is_aborted_before_joining_realtime_groups()
    {
        await using var db = TestDb.Create();
        var tenantId = Guid.NewGuid();
        const string userId = "locked-realtime-user";

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Realtime Tenant",
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow
        });
        db.Stores.Add(new Store
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Realtime Store",
            Code = "RT",
            Status = StoreStatuses.Active,
            CreatedAt = DateTime.UtcNow
        });
        db.AspNetUsers.Add(new AspNetUser
        {
            Id = userId,
            UserName = "locked@realtime.local",
            NormalizedUserName = "LOCKED@REALTIME.LOCAL",
            Email = "locked@realtime.local",
            NormalizedEmail = "LOCKED@REALTIME.LOCAL",
            TenantId = tenantId,
            Status = UserStatuses.Locked,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(AppClaimTypes.TenantId, tenantId.ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Owner)
        }, "Test"));
        var context = new RecordingHubCallerContext(principal);
        var groups = new RecordingGroupManager();
        var hub = new ChainPosHub(db)
        {
            Context = context,
            Groups = groups
        };

        await hub.OnConnectedAsync();

        Assert.True(context.Aborted);
        Assert.Empty(groups.AddedGroups);
    }

    private sealed class RecordingHubCallerContext : HubCallerContext
    {
        public RecordingHubCallerContext(ClaimsPrincipal user)
        {
            User = user;
        }

        public bool Aborted { get; private set; }

        public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");

        public override string? UserIdentifier { get; } = "test-user";

        public override ClaimsPrincipal? User { get; }

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted { get; } = CancellationToken.None;

        public override void Abort()
        {
            Aborted = true;
        }
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<string> AddedGroups { get; } = new();

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            AddedGroups.Add(groupName);
            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
