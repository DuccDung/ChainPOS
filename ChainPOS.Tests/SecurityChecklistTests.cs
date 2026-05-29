using ChainPOS.Constants;
using ChainPOS.Filters;
using ChainPOS.Models;
using ChainPOS.Services.Owner;
using ChainPOS.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
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
}
