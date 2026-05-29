using ChainPOS.Constants;
using ChainPOS.Services.Inventory;
using ChainPOS.Services.Sales;
using ChainPOS.Services.Security;
using ChainPOS.Tests.TestSupport;
using ChainPOS.ViewModels.Inventory;
using ChainPOS.ViewModels.Sales;
using Xunit;

namespace ChainPOS.Tests;

public sealed class SalesAndInventoryValidationTests
{
    [Fact]
    public async Task Import_stock_rejects_non_positive_quantity()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var service = new InventoryService(db, currentUser, storeAccess, new FakeAuditLogService());

        var result = await service.ImportStockAsync(new InventoryMovementViewModel
        {
            StoreId = seed.StoreId,
            ProductId = seed.ProductId,
            Quantity = 0m,
            MinQuantity = 1m
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Quantity must be greater than 0.", result.Error);
    }

    [Fact]
    public async Task Import_stock_creates_inventory_and_writes_transaction()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var audit = new FakeAuditLogService();
        var service = new InventoryService(db, currentUser, storeAccess, audit);

        var result = await service.ImportStockAsync(new InventoryMovementViewModel
        {
            StoreId = seed.StoreId,
            ProductId = seed.ProductId,
            Quantity = 5m,
            MinQuantity = 2m,
            Reason = "Opening stock"
        });

        Assert.True(result.Succeeded);
        var inventory = db.Inventories.Single();
        Assert.Equal(5m, inventory.Quantity);
        Assert.Equal(2m, inventory.MinQuantity);
        Assert.Contains("ImportStock", audit.Actions);
        var transaction = db.InventoryTransactions.Single();
        Assert.Equal(InventoryTransactionTypes.Import, transaction.Type);
        Assert.Equal(0m, transaction.BeforeQuantity);
        Assert.Equal(5m, transaction.AfterQuantity);
    }

    [Fact]
    public async Task Export_stock_decreases_inventory_and_writes_transaction()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        await TestDb.SeedInventoryAsync(db, seed.TenantId, seed.StoreId, seed.ProductId, quantity: 5m, updatedBy: seed.OwnerId);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var audit = new FakeAuditLogService();
        var service = new InventoryService(db, currentUser, storeAccess, audit);

        var result = await service.ExportStockAsync(new InventoryMovementViewModel
        {
            StoreId = seed.StoreId,
            ProductId = seed.ProductId,
            Quantity = 2m,
            Reason = "Transfer to warehouse"
        });

        Assert.True(result.Succeeded);
        Assert.Equal(3m, db.Inventories.Single().Quantity);
        Assert.Contains("ExportStock", audit.Actions);
        var transaction = db.InventoryTransactions.Single();
        Assert.Equal(InventoryTransactionTypes.Export, transaction.Type);
        Assert.Equal(5m, transaction.BeforeQuantity);
        Assert.Equal(3m, transaction.AfterQuantity);
    }

    [Fact]
    public async Task Adjust_stock_sets_actual_quantity_and_writes_transaction()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        await TestDb.SeedInventoryAsync(db, seed.TenantId, seed.StoreId, seed.ProductId, quantity: 5m, updatedBy: seed.OwnerId);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var audit = new FakeAuditLogService();
        var service = new InventoryService(db, currentUser, storeAccess, audit);

        var result = await service.AdjustStockAsync(new InventoryAdjustViewModel
        {
            StoreId = seed.StoreId,
            ProductId = seed.ProductId,
            ActualQuantity = 7m,
            MinQuantity = 2m,
            Reason = "Cycle count"
        });

        Assert.True(result.Succeeded);
        var inventory = db.Inventories.Single();
        Assert.Equal(7m, inventory.Quantity);
        Assert.Equal(2m, inventory.MinQuantity);
        Assert.Contains("AdjustStock", audit.Actions);
        var transaction = db.InventoryTransactions.Single();
        Assert.Equal(InventoryTransactionTypes.Adjust, transaction.Type);
        Assert.Equal(2m, transaction.Quantity);
        Assert.Equal(5m, transaction.BeforeQuantity);
        Assert.Equal(7m, transaction.AfterQuantity);
    }

    [Fact]
    public async Task Checkout_requires_open_shift()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var service = new PosService(db, currentUser, storeAccess, new FakeAuditLogService());

        var result = await service.CheckoutAsync(new PosCheckoutInputModel
        {
            StoreId = seed.StoreId,
            PaymentMethod = PaymentMethods.Cash,
            Items = new List<PosCartItemInputModel>
            {
                new() { ProductId = seed.ProductId, Quantity = 1m }
            }
        });

        Assert.False(result.Succeeded);
        Assert.Equal("You must open a shift before checkout.", result.Error);
    }

    [Fact]
    public async Task Checkout_creates_order_and_decreases_inventory_using_store_price()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        await TestDb.SeedInventoryAsync(db, seed.TenantId, seed.StoreId, seed.ProductId, quantity: 5m, updatedBy: seed.OwnerId);
        db.StoreProducts.Single().SellingPrice = 8m;
        await db.SaveChangesAsync();
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var audit = new FakeAuditLogService();
        var shiftService = new ShiftService(db, currentUser, storeAccess, audit);
        var posService = new PosService(db, currentUser, storeAccess, audit);

        var openShift = await shiftService.OpenShiftAsync(new ShiftOpenViewModel
        {
            StoreId = seed.StoreId,
            OpeningCash = 20m
        });
        var result = await posService.CheckoutAsync(new PosCheckoutInputModel
        {
            StoreId = seed.StoreId,
            PaymentMethod = PaymentMethods.Cash,
            CustomerPaidAmount = 20m,
            Items = new List<PosCartItemInputModel>
            {
                new() { ProductId = seed.ProductId, Quantity = 2m }
            }
        });

        Assert.True(openShift.Succeeded);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.OrderId);
        Assert.Equal(3m, db.Inventories.Single().Quantity);
        var order = db.Orders.Single();
        Assert.Equal(16m, order.SubTotal);
        Assert.Equal(16m, order.TotalAmount);
        Assert.Equal(openShift.ShiftId, order.ShiftId);
        Assert.Equal(8m, db.OrderItems.Single().UnitPrice);
        Assert.Equal(16m, db.Payments.Single().Amount);
        Assert.Contains("CreateOrder", audit.Actions);
        Assert.Equal(InventoryTransactionTypes.Sale, db.InventoryTransactions.Single().Type);
    }

    [Fact]
    public async Task Cancel_order_returns_not_found_for_missing_order()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var service = new OrderService(db, currentUser, storeAccess, new FakeAuditLogService());

        var result = await service.CancelOrderAsync(Guid.NewGuid());

        Assert.False(result.Succeeded);
        Assert.Equal("Order not found.", result.Error);
    }

    [Fact]
    public async Task Cancel_order_restores_inventory_and_marks_payment_cancelled()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        await TestDb.SeedInventoryAsync(db, seed.TenantId, seed.StoreId, seed.ProductId, quantity: 5m, updatedBy: seed.OwnerId);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var audit = new FakeAuditLogService();
        var shiftService = new ShiftService(db, currentUser, storeAccess, audit);
        var posService = new PosService(db, currentUser, storeAccess, audit);
        var orderService = new OrderService(db, currentUser, storeAccess, audit);

        await shiftService.OpenShiftAsync(new ShiftOpenViewModel
        {
            StoreId = seed.StoreId,
            OpeningCash = 0m
        });
        var checkout = await posService.CheckoutAsync(new PosCheckoutInputModel
        {
            StoreId = seed.StoreId,
            PaymentMethod = PaymentMethods.Cash,
            CustomerPaidAmount = 10m,
            Items = new List<PosCartItemInputModel>
            {
                new() { ProductId = seed.ProductId, Quantity = 1m }
            }
        });
        var result = await orderService.CancelOrderAsync(checkout.OrderId!.Value);

        Assert.True(result.Succeeded);
        Assert.Equal(5m, db.Inventories.Single().Quantity);
        Assert.Equal(OrderStatuses.Cancelled, db.Orders.Single().OrderStatus);
        Assert.Equal(OrderPaymentStatuses.Cancelled, db.Orders.Single().PaymentStatus);
        Assert.Equal(PaymentStatuses.Cancelled, db.Payments.Single().Status);
        Assert.Contains("CancelOrder", audit.Actions);
        Assert.Contains(db.InventoryTransactions, x => x.Type == InventoryTransactionTypes.Return);
    }

    [Fact]
    public async Task Close_shift_rejects_negative_closing_cash()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var service = new ShiftService(db, currentUser, storeAccess, new FakeAuditLogService());

        var result = await service.CloseShiftAsync(Guid.NewGuid(), new ShiftCloseViewModel
        {
            ClosingCash = -1m
        });

        Assert.False(result.Succeeded);
        Assert.Equal("Closing cash must be greater than or equal to 0.", result.Error);
    }

    [Fact]
    public async Task Close_shift_calculates_expected_cash_and_difference()
    {
        await using var db = TestDb.Create();
        var seed = await TestDb.SeedTenantStoreProductAsync(db);
        await TestDb.SeedInventoryAsync(db, seed.TenantId, seed.StoreId, seed.ProductId, quantity: 5m, updatedBy: seed.OwnerId);
        var currentUser = OwnerUser(seed.TenantId, seed.OwnerId);
        var storeAccess = new StoreAccessService(db, currentUser);
        var audit = new FakeAuditLogService();
        var shiftService = new ShiftService(db, currentUser, storeAccess, audit);
        var posService = new PosService(db, currentUser, storeAccess, audit);

        var openShift = await shiftService.OpenShiftAsync(new ShiftOpenViewModel
        {
            StoreId = seed.StoreId,
            OpeningCash = 20m
        });
        await posService.CheckoutAsync(new PosCheckoutInputModel
        {
            StoreId = seed.StoreId,
            PaymentMethod = PaymentMethods.Cash,
            CustomerPaidAmount = 10m,
            Items = new List<PosCartItemInputModel>
            {
                new() { ProductId = seed.ProductId, Quantity = 1m }
            }
        });
        var result = await shiftService.CloseShiftAsync(openShift.ShiftId!.Value, new ShiftCloseViewModel
        {
            ClosingCash = 35m
        });

        Assert.True(result.Succeeded);
        var shift = db.Shifts.Single();
        Assert.Equal(ShiftStatuses.Closed, shift.Status);
        Assert.Equal(30m, shift.ExpectedCash);
        Assert.Equal(5m, shift.DifferenceAmount);
        Assert.Contains("CloseShift", audit.Actions);
    }

    private static FakeCurrentUserService OwnerUser(Guid tenantId, string userId)
        => new()
        {
            UserId = userId,
            TenantId = tenantId,
            Roles = new[] { AppRoles.Owner }
        };
}
