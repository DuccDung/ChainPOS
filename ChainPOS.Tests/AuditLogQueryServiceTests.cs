using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Tests.TestSupport;
using ChainPOS.ViewModels.Audit;
using Xunit;

namespace ChainPOS.Tests;

public sealed class AuditLogQueryServiceTests
{
    [Fact]
    public async Task Owner_audit_query_handles_large_filtered_dataset_with_tenant_scope_and_paging()
    {
        await using var db = TestDb.Create();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Tenants.AddRange(
            new Tenant
            {
                Id = tenantId,
                Name = "Audit Tenant",
                Status = TenantStatuses.Active,
                CreatedAt = now
            },
            new Tenant
            {
                Id = otherTenantId,
                Name = "Other Tenant",
                Status = TenantStatuses.Active,
                CreatedAt = now
            });
        db.Stores.Add(new Store
        {
            Id = storeId,
            TenantId = tenantId,
            Name = "Main Store",
            Code = "MAIN",
            Status = StoreStatuses.Active,
            CreatedAt = now
        });
        db.AspNetUsers.AddRange(
            CreateUser("audit-owner-1", tenantId, now),
            CreateUser("audit-owner-2", tenantId, now),
            CreateUser("audit-other", otherTenantId, now));

        var logs = new List<AuditLog>();
        for (var i = 0; i < 1200; i++)
        {
            logs.Add(new AuditLog
            {
                Id = i + 1,
                TenantId = tenantId,
                StoreId = storeId,
                UserId = i % 8 == 0 ? "audit-owner-1" : "audit-owner-2",
                Action = i % 4 == 0 ? "UpdateProduct" : i % 4 == 1 ? "CancelOrder" : i % 4 == 2 ? "Login" : "LockStaff",
                EntityName = nameof(Product),
                EntityId = i.ToString(),
                NewValue = $"Product={i}",
                CreatedAt = now.AddMinutes(-i)
            });
        }

        for (var i = 0; i < 500; i++)
        {
            logs.Add(new AuditLog
            {
                Id = 2000 + i,
                TenantId = otherTenantId,
                UserId = "audit-other",
                Action = "UpdateProduct",
                EntityName = nameof(Product),
                EntityId = $"other-{i}",
                NewValue = $"OtherProduct={i}",
                CreatedAt = now.AddMinutes(-i)
            });
        }

        db.AuditLogs.AddRange(logs);
        await db.SaveChangesAsync();

        var currentUser = new FakeCurrentUserService
        {
            UserId = "audit-owner-1",
            TenantId = tenantId,
            Roles = new[] { AppRoles.Owner }
        };
        var service = new AuditLogQueryService(db, currentUser);

        var model = await service.GetAuditLogsAsync("Owner", new AuditLogFilterViewModel
        {
            Action = "UpdateProduct",
            FromDate = DateOnly.FromDateTime(now.AddDays(-2)),
            ToDate = DateOnly.FromDateTime(now.AddDays(1)),
            Page = 1,
            PageSize = 200
        });

        Assert.Equal(300, model.TotalEvents);
        Assert.Equal(100, model.PageSize);
        Assert.Equal(3, model.TotalPages);
        Assert.Equal(100, model.Logs.Count);
        Assert.Equal(2, model.DistinctUsers);
        Assert.All(model.Logs, log =>
        {
            Assert.Equal(tenantId, log.TenantId);
            Assert.Equal("UpdateProduct", log.Action);
        });
    }

    private static AspNetUser CreateUser(string id, Guid tenantId, DateTime now)
        => new()
        {
            Id = id,
            UserName = $"{id}@audit.local",
            NormalizedUserName = $"{id}@AUDIT.LOCAL".ToUpperInvariant(),
            Email = $"{id}@audit.local",
            NormalizedEmail = $"{id}@AUDIT.LOCAL".ToUpperInvariant(),
            FullName = id,
            TenantId = tenantId,
            Status = UserStatuses.Active,
            CreatedAt = now
        };
}
