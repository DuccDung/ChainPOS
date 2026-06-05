using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Auth;

public sealed class RefreshUserClaimsCookieEvents : CookieAuthenticationEvents
{
    private readonly StoreFlowDbContext _db;

    public RefreshUserClaimsCookieEvents(StoreFlowDbContext db)
    {
        _db = db;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await RejectPrincipalAsync(context);
            return;
        }

        var user = await _db.AspNetUsers
            .AsNoTracking()
            .Include(x => x.Roles)
            .Include(x => x.Tenant)
            .Where(x => x.Id == userId)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            await RejectPrincipalAsync(context);
            return;
        }

        if (!IsUserAllowed(user))
        {
            await RejectPrincipalAsync(context);
            return;
        }

        var stampClaim = context.Principal?.FindFirstValue(AppClaimTypes.SecurityStamp);
        if (!string.Equals(stampClaim, user.SecurityStamp, StringComparison.Ordinal))
        {
            await RejectPrincipalAsync(context);
            return;
        }

        var roleNames = user.Roles
            .Select(x => x.Name ?? x.NormalizedName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roleNames.Length == 0)
        {
            await RejectPrincipalAsync(context);
            return;
        }

        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var changed = false;
        changed |= SetClaim(identity, AppClaimTypes.FullName, user.FullName);
        changed |= SetClaim(identity, ClaimTypes.Email, user.Email);
        changed |= SetClaim(identity, AppClaimTypes.SecurityStamp, user.SecurityStamp);
        changed |= SetClaim(identity, AppClaimTypes.TenantId, user.TenantId?.ToString());
        changed |= SetClaims(identity, ClaimTypes.Role, roleNames);

        if (changed)
        {
            context.ReplacePrincipal(new ClaimsPrincipal(identity));
            context.ShouldRenew = true;
        }
    }

    private static bool IsUserAllowed(AspNetUser user)
    {
        if (!string.Equals(user.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
        {
            return false;
        }

        var isAdmin = user.Roles.Any(x => string.Equals(x.Id, AppRoles.Admin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Name, AppRoles.Admin, StringComparison.OrdinalIgnoreCase));
        if (isAdmin)
        {
            return true;
        }

        if (!user.TenantId.HasValue || user.Tenant is null || user.Tenant.IsDeleted)
        {
            return false;
        }

        return !string.Equals(user.Tenant.Status, TenantStatuses.Suspended, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(user.Tenant.Status, TenantStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SetClaim(ClaimsIdentity identity, string type, string? value)
    {
        var claims = identity.FindAll(type).ToArray();
        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var claim in claims)
            {
                identity.RemoveClaim(claim);
            }

            return claims.Length > 0;
        }

        if (claims.Length == 1 && claims[0].Value == value)
        {
            return false;
        }

        foreach (var claim in claims)
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(type, value));
        return true;
    }

    private static bool SetClaims(ClaimsIdentity identity, string type, IReadOnlyCollection<string> values)
    {
        var normalizedValues = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = identity.FindAll(type)
            .Select(x => x.Value)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (existing.SequenceEqual(normalizedValues, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var claim in identity.FindAll(type).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        foreach (var value in normalizedValues)
        {
            identity.AddClaim(new Claim(type, value));
        }

        return true;
    }

    private static async Task RejectPrincipalAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
