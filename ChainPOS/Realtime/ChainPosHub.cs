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

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)
            || !await IsUserAllowedToConnectAsync(userId, user.IsInRole(AppRoles.Admin)))
        {
            Context.Abort();
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
            var ownerStoreIds = await _db.Stores
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId.Value
                    && !x.IsDeleted
                    && x.Status == StoreStatuses.Active)
                .Select(x => x.Id)
                .ToListAsync(Context.ConnectionAborted);

            foreach (var storeId in ownerStoreIds)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Store(tenantId.Value, storeId));
            }
        }
        else if (user.IsInRole(AppRoles.Staff))
        {
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

    private async Task<bool> IsUserAllowedToConnectAsync(string userId, bool isAdmin)
    {
        var user = await _db.AspNetUsers
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.Status,
                x.LockoutEnd,
                x.TenantId,
                TenantStatus = x.Tenant != null ? x.Tenant.Status : null,
                TenantIsDeleted = x.Tenant != null && x.Tenant.IsDeleted
            })
            .FirstOrDefaultAsync(Context.ConnectionAborted);

        if (user is null)
        {
            return false;
        }

        if (!string.Equals(user.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return false;
        }

        if (isAdmin)
        {
            return true;
        }

        if (!user.TenantId.HasValue || user.TenantIsDeleted)
        {
            return false;
        }

        return !string.Equals(user.TenantStatus, TenantStatuses.Suspended, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(user.TenantStatus, TenantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? GetTenantId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(AppClaimTypes.TenantId);
        return Guid.TryParse(value, out var tenantId) ? tenantId : null;
    }
}
