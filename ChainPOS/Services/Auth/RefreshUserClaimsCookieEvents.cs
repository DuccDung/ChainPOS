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
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.FullName
            })
            .FirstOrDefaultAsync();

        if (user is null)
        {
            await RejectPrincipalAsync(context);
            return;
        }

        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        if (SetClaim(identity, AppClaimTypes.FullName, user.FullName))
        {
            context.ReplacePrincipal(new ClaimsPrincipal(identity));
            context.ShouldRenew = true;
        }
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

    private static async Task RejectPrincipalAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
