using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Filters;

public sealed class RequireTenantFilter : IAsyncAuthorizationFilter
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RequireTenantFilter(StoreFlowDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.Filters.Any(x => x is IAllowAnonymousFilter))
        {
            return;
        }

        if (!_currentUser.IsAuthenticated || _currentUser.IsInRole(AppRoles.Admin))
        {
            return;
        }

        if (!_currentUser.IsInRole(AppRoles.Owner) && !_currentUser.IsInRole(AppRoles.Staff))
        {
            return;
        }

        if (!_currentUser.TenantId.HasValue)
        {
            context.Result = new ForbidResult();
            return;
        }

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == _currentUser.TenantId.Value);

        if (tenant is null
            || tenant.IsDeleted
            || tenant.Status == TenantStatuses.Suspended
            || tenant.Status == TenantStatuses.Cancelled)
        {
            context.Result = new ForbidResult();
        }
    }
}
