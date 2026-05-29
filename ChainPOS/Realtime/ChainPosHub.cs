using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Realtime;

[Authorize]
public sealed class ChainPosHub : Hub
{
    private readonly StoreFlowDbContext _db;

    public ChainPosHub(StoreFlowDbContext db)
    {
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await base.OnConnectedAsync();
            return;
        }

        if (user.IsInRole(AppRoles.Admin))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.PlatformAdmins);
            await base.OnConnectedAsync();
            return;
        }

        var tenantId = GetTenantId(user);
        if (!tenantId.HasValue)
        {
            await base.OnConnectedAsync();
            return;
        }

        if (user.IsInRole(AppRoles.Owner))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Tenant(tenantId.Value));
        }
        else if (user.IsInRole(AppRoles.Staff))
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var storeIds = await _db.UserStores
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId.Value
                        && x.UserId == userId
                        && x.IsActive
                        && !x.Store.IsDeleted
                        && x.Store.Status == StoreStatuses.Active)
                    .Select(x => x.StoreId)
                    .ToListAsync(Context.ConnectionAborted);

                foreach (var storeId in storeIds)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Store(tenantId.Value, storeId));
                }
            }
        }

        await base.OnConnectedAsync();
    }

    private static Guid? GetTenantId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(AppClaimTypes.TenantId);
        return Guid.TryParse(value, out var tenantId) ? tenantId : null;
    }
}
