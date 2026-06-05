using System.Text;
using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Admin;
using ChainPOS.Services.Import;
using ChainPOS.Services.Inventory;
using ChainPOS.Services.Owner;
using ChainPOS.Services.Security;
using ChainPOS.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace ChainPOS.Tests;

public sealed class BulkImportServiceTests
{
    [Fact]
    public async Task Import_categories_creates_categories_and_summary_audit()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var audit = new FakeAuditLogService();
        var service = CreateService(db, currentUser, audit);

        var result = await service.ImportCategoriesAsync(CsvFile(
            "Name,Description,IsActive\nImported Category,Imported by CSV,true\n"));

        Assert.Equal(1, result.SuccessRows);
        Assert.Contains(db.Categories, x => x.TenantId == seed.TenantId && x.Name == "Imported Category");
        Assert.Contains("CreateCategory", audit.Actions);
        Assert.Contains("BulkImportCategories", audit.Actions);
    }

    [Fact]
    public async Task Import_store_products_assigns_by_store_code_and_sku()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var secondStoreId = Guid.NewGuid();
        db.Stores.Add(new Store
        {
            Id = secondStoreId,
            TenantId = seed.TenantId,
            Name = "Second Store",
            Code = "SECOND",
            Status = StoreStatuses.Active,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var audit = new FakeAuditLogService();
        var service = CreateService(db, currentUser, audit);

        var result = await service.ImportStoreProductsAsync(CsvFile(
            "StoreCode,Sku,SellingPrice,IsAvailable\nSECOND,TEST-001,12.50,true\n"));

        Assert.Equal(1, result.SuccessRows);
        Assert.Contains(db.StoreProducts, x => x.StoreId == secondStoreId
            && x.ProductId == seed.ProductId
            && x.SellingPrice == 12.50m
            && x.IsAvailable);
        Assert.Contains("AssignStoreProduct", audit.Actions);
        Assert.Contains("BulkImportStoreProducts", audit.Actions);
    }

    private static BulkImportService CreateService(
        StoreFlowDbContext db,
        FakeCurrentUserService currentUser,
        FakeAuditLogService audit)
    {
        var storeAccess = new StoreAccessService(db, currentUser);
        var realtime = new FakeRealtimeNotifier();
        return new BulkImportService(
            db,
            new AdminManagementService(db, new PasswordHasher<AspNetUser>(), audit),
            new OwnerStaffService(db, currentUser, new PasswordHasher<AspNetUser>(), audit),
            new OwnerStoreService(db, currentUser, audit),
            new OwnerCategoryService(db, currentUser, audit),
            new OwnerProductService(db, currentUser, audit, new FakeWebHostEnvironment()),
            new OwnerStoreProductService(db, currentUser, audit),
            new InventoryService(db, currentUser, storeAccess, audit, realtime),
            currentUser,
            audit);
    }

    private static IFormFile CsvFile(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "import.csv");
    }

    private static FakeCurrentUserService OwnerUser(Guid tenantId, string userId)
        => new()
        {
            UserId = userId,
            TenantId = tenantId,
            Roles = new[] { AppRoles.Owner }
        };

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ChainPOS.Tests";

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = Path.GetTempPath();

        public string EnvironmentName { get; set; } = "Development";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
