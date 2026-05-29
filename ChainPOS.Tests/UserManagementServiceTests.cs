using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Admin;
using ChainPOS.Services.Owner;
using ChainPOS.Tests.TestSupport;
using ChainPOS.ViewModels.Admin.Owners;
using ChainPOS.ViewModels.Owner.Staff;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChainPOS.Tests;

public sealed class UserManagementServiceTests
{
    [Fact]
    public async Task Admin_create_owner_creates_owner_role_and_tenant()
    {
        await using var db = TestDb.Create();
        var audit = new FakeAuditLogService();
        var service = new AdminManagementService(db, new PasswordHasher<AspNetUser>(), audit);

        var result = await service.CreateOwnerAsync(new OwnerCreateViewModel
        {
            FullName = "Owner Test",
            Email = "owner.create@test.local",
            TenantName = "Owner Tenant",
            Password = "Owner@123",
            ConfirmPassword = "Owner@123"
        }, "admin-test");

        Assert.True(result.Succeeded, result.Error);

        var owner = await db.AspNetUsers
            .Include(x => x.Roles)
            .SingleAsync(x => x.Email == "owner.create@test.local");
        var tenant = await db.Tenants.SingleAsync(x => x.OwnerUserId == owner.Id);

        Assert.Equal(UserStatuses.Active, owner.Status);
        Assert.Equal(tenant.Id, owner.TenantId);
        Assert.Contains(owner.Roles, x => x.Id == AppRoles.Owner);
        Assert.Equal(TenantStatuses.Active, tenant.Status);
        Assert.Contains("CreateUser", audit.Actions);
        Assert.Contains("CreateTenant", audit.Actions);
    }

    [Fact]
    public async Task Owner_create_staff_creates_staff_role_and_active_store_assignment()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var currentUser = new FakeCurrentUserService
        {
            UserId = seed.OwnerId,
            TenantId = seed.TenantId,
            Roles = new[] { AppRoles.Owner }
        };
        var audit = new FakeAuditLogService();
        var service = new OwnerStaffService(db, currentUser, new PasswordHasher<AspNetUser>(), audit);

        var result = await service.CreateStaffAsync(new StaffCreateViewModel
        {
            FullName = "Staff Created",
            Email = "staff.create@test.local",
            Password = "Staff@123",
            ConfirmPassword = "Staff@123",
            StoreIds = new List<Guid> { seed.StoreId }
        });

        Assert.True(result.Succeeded, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.StaffId));

        var staff = await db.AspNetUsers
            .Include(x => x.Roles)
            .SingleAsync(x => x.Id == result.StaffId);
        var assignment = await db.UserStores.SingleAsync(x => x.UserId == staff.Id && x.StoreId == seed.StoreId);

        Assert.Equal(seed.TenantId, staff.TenantId);
        Assert.Equal(UserStatuses.Active, staff.Status);
        Assert.Contains(staff.Roles, x => x.Id == AppRoles.Staff);
        Assert.True(assignment.IsActive);
        Assert.Contains("CreateStaff", audit.Actions);
    }
}
