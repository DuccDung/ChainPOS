using ChainPOS.Constants;
using ChainPOS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ChainPOS.Tests.TestSupport;

internal static class TestDb
{
    public static StoreFlowDbContext Create()
    {
        var options = new DbContextOptionsBuilder<StoreFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new StoreFlowDbContext(options);
    }

    public static async Task<(Guid TenantId, Guid StoreId, Guid ProductId, string OwnerId, string StaffId)> SeedTenantStoreProductAsync(StoreFlowDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        const string ownerId = "owner-test";
        const string staffId = "staff-test";

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Test Tenant",
            Status = TenantStatuses.Active,
            CreatedAt = DateTime.UtcNow
        });
        db.Stores.Add(new Store
        {
            Id = storeId,
            TenantId = tenantId,
            Name = "Main Store",
            Code = "MAIN",
            Status = StoreStatuses.Active,
            CreatedAt = DateTime.UtcNow
        });
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "General",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            CategoryId = categoryId,
            Name = "Test Product",
            Sku = "TEST-001",
            Price = 10m,
            CostPrice = 5m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.StoreProducts.Add(new StoreProduct
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StoreId = storeId,
            ProductId = productId,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow
        });
        db.UserStores.Add(new UserStore
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StoreId = storeId,
            UserId = staffId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return (tenantId, storeId, productId, ownerId, staffId);
    }

    public static async Task SeedInventoryAsync(
        StoreFlowDbContext db,
        Guid tenantId,
        Guid storeId,
        Guid productId,
        decimal quantity = 5m,
        decimal minQuantity = 1m,
        string? updatedBy = null)
    {
        db.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StoreId = storeId,
            ProductId = productId,
            Quantity = quantity,
            MinQuantity = minQuantity,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy
        });

        await db.SaveChangesAsync();
    }
}
