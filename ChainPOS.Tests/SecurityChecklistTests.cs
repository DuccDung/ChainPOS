using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Filters;
using ChainPOS.Models;
using ChainPOS.Services.Auth;
using ChainPOS.Services.Owner;
using ChainPOS.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChainPOS.Tests;

public sealed class SecurityChecklistTests
{
    [Fact]
    public async Task Owner_store_list_only_returns_current_tenant_stores()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        db.Stores.Add(new Store
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Other Tenant Store",
            Code = "OTHER",
            Status = StoreStatuses.Active,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var currentUser = new FakeCurrentUserService
        {
            UserId = seed.OwnerId,
            TenantId = seed.TenantId,
            Roles = new[] { AppRoles.Owner }
        };
        var service = new OwnerStoreService(db, currentUser, new FakeAuditLogService());

        var model = await service.GetStoresAsync(null, null);

        Assert.Single(model.Stores);
        Assert.DoesNotContain(model.Stores, x => x.Code == "OTHER");
    }

    [Fact]
    public async Task Suspended_tenant_is_forbidden_by_tenant_filter()
    {
        await using var db = TestDb.Create();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Suspended Tenant",
            Status = TenantStatuses.Suspended,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var currentUser = new FakeCurrentUserService
        {
            UserId = "owner-suspended",
            TenantId = tenantId,
            Roles = new[] { AppRoles.Owner }
        };
        var filter = new RequireTenantFilter(db, currentUser);
        var context = new AuthorizationFilterContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());

        await filter.OnAuthorizationAsync(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public async Task Locked_user_cookie_is_rejected_and_signed_out_on_next_request()
    {
        await using var db = TestDb.Create();
        var tenantId = Guid.NewGuid();
        const string userId = "locked-cookie-user";
        const string securityStamp = "stamp-before-lock";

        var role = new AspNetRole
        {
            Id = AppRoles.Owner,
            Name = AppRoles.Owner,
            NormalizedName = AppRoles.Owner.ToUpperInvariant()
        };
        var user = new AspNetUser
        {
            Id = userId,
            UserName = "owner@locked.local",
            NormalizedUserName = "OWNER@LOCKED.LOCAL",
            Email = "owner@locked.local",
            NormalizedEmail = "OWNER@LOCKED.LOCAL",
            FullName = "Locked Owner",
            TenantId = tenantId,
            Status = UserStatuses.Locked,
            SecurityStamp = securityStamp,
            CreatedAt = DateTime.UtcNow
        };
        user.Roles.Add(role);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Active Tenant",
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow
        });
        db.AspNetRoles.Add(role);
        db.AspNetUsers.Add(user);
        await db.SaveChangesAsync();

        var authService = new RecordingAuthenticationService();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(authService)
                .BuildServiceProvider()
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(AppClaimTypes.SecurityStamp, securityStamp),
            new Claim(ClaimTypes.Role, AppRoles.Owner)
        }, CookieAuthenticationDefaults.AuthenticationScheme));
        var ticket = new AuthenticationTicket(
            principal,
            new AuthenticationProperties(),
            CookieAuthenticationDefaults.AuthenticationScheme);
        var context = new CookieValidatePrincipalContext(
            httpContext,
            new AuthenticationScheme(
                CookieAuthenticationDefaults.AuthenticationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme,
                typeof(CookieAuthenticationHandler)),
            new CookieAuthenticationOptions(),
            ticket);
        var events = new RefreshUserClaimsCookieEvents(db);

        await events.ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.Contains(CookieAuthenticationDefaults.AuthenticationScheme, authService.SignOutSchemes);
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public List<string?> SignOutSchemes { get; } = new();

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context,
            string? scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutSchemes.Add(scheme);
            return Task.CompletedTask;
        }
    }
}
