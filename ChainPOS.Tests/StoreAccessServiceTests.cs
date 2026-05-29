using ChainPOS.Constants;
using ChainPOS.Services.Security;
using ChainPOS.Tests.TestSupport;
using Xunit;

namespace ChainPOS.Tests;

public sealed class StoreAccessServiceTests
{
    [Fact]
    public async Task Owner_can_access_active_store_in_own_tenant()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var currentUser = new FakeCurrentUserService
        {
            UserId = seed.OwnerId,
            TenantId = seed.TenantId,
            Roles = new[] { AppRoles.Owner }
        };

        var service = new StoreAccessService(db, currentUser);

        Assert.True(await service.CanAccessStoreAsync(seed.StoreId));
    }

    [Fact]
    public async Task Staff_without_active_assignment_cannot_access_store()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var assignment = db.UserStores.Single();
        assignment.IsActive = false;
        await db.SaveChangesAsync();

        var currentUser = new FakeCurrentUserService
        {
            UserId = seed.StaffId,
            TenantId = seed.TenantId,
            Roles = new[] { AppRoles.Staff }
        };
        var service = new StoreAccessService(db, currentUser);

        Assert.False(await service.CanAccessStoreAsync(seed.StoreId));
    }
}
