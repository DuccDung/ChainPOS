using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.Services.Realtime;
using ChainPOS.Services.Security;
using ChainPOS.ViewModels.Sales;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Sales;

public sealed class PosService : IPosService
{
    private static readonly HashSet<string> AllowedPaymentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        PaymentMethods.Cash,
        PaymentMethods.BankTransfer,
        PaymentMethods.Card,
        PaymentMethods.Momo,
        PaymentMethods.ZaloPay,
        PaymentMethods.Other
    };

    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IStoreAccessService _storeAccess;
    private readonly IAuditLogService _auditLog;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public PosService(
        StoreFlowDbContext db,
        ICurrentUserService currentUser,
        IStoreAccessService storeAccess,
        IAuditLogService auditLog,
        IRealtimeNotifier realtimeNotifier)
    {
        _db = db;
        _currentUser = currentUser;
        _storeAccess = storeAccess;
        _auditLog = auditLog;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<PosIndexViewModel> GetRegisterAsync(
        string areaName,
        Guid? storeId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var accessibleStoreIds = await _storeAccess.GetAccessibleStoreIdsAsync(cancellationToken);
        var stores = await GetStoreOptionsAsync(accessibleStoreIds, cancellationToken);
        var selectedStoreId = storeId;

        if (!selectedStoreId.HasValue && stores.Count > 0)
        {
            selectedStoreId = stores[0].Id;
        }

        var model = new PosIndexViewModel
        {
            AreaName = areaName,
            StoreId = selectedStoreId,
            Search = search?.Trim(),
            Stores = stores
        };

        if (!selectedStoreId.HasValue || !accessibleStoreIds.Contains(selectedStoreId.Value))
        {
            return model;
        }

        var openShift = await _db.Shifts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.StoreId == selectedStoreId.Value
                && x.OpenedBy == userId
                && x.Status == ShiftStatuses.Open)
            .OrderByDescending(x => x.OpenedAt)
            .Select(x => new { x.Id, x.OpenedAt, StoreName = x.Store.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (openShift is not null)
        {
            model.OpenShiftId = openShift.Id;
            model.OpenShiftOpenedAt = openShift.OpenedAt;
            model.OpenShiftStoreName = openShift.StoreName;
        }

        var productsQuery = _db.StoreProducts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.StoreId == selectedStoreId.Value
                && x.IsAvailable
                && !x.Product.IsDeleted
                && x.Product.IsActive);

        if (!string.IsNullOrWhiteSpace(model.Search))
        {
            productsQuery = productsQuery.Where(x =>
                x.Product.Name.Contains(model.Search) ||
                (x.Product.Sku != null && x.Product.Sku.Contains(model.Search)) ||
                (x.Product.Barcode != null && x.Product.Barcode.Contains(model.Search)));
        }

        model.Products = await productsQuery
            .OrderBy(x => x.Product.Name)
            .Select(x => new PosProductViewModel
            {
                ProductId = x.ProductId,
                Name = x.Product.Name,
                Sku = x.Product.Sku,
                Barcode = x.Product.Barcode,
                CategoryName = x.Product.Category != null ? x.Product.Category.Name : null,
                ImageUrl = x.Product.ImageUrl,
                Price = x.SellingPrice ?? x.Product.Price,
                QuantityOnHand = _db.Inventories
                    .Where(i => i.TenantId == tenantId && i.StoreId == x.StoreId && i.ProductId == x.ProductId)
                    .Select(i => i.Quantity)
                    .FirstOrDefault(),
                IsLowStock = _db.Inventories
                    .Where(i => i.TenantId == tenantId && i.StoreId == x.StoreId && i.ProductId == x.ProductId)
                    .Select(i => i.Quantity > 0 && i.Quantity <= i.MinQuantity)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        model.PendingOrders = await _db.Orders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.StoreId == selectedStoreId.Value
                && x.OrderStatus == OrderStatuses.New
                && x.PaymentStatus == OrderPaymentStatuses.Unpaid)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new PosPendingOrderViewModel
            {
                Id = x.Id,
                OrderCode = x.OrderCode,
                ItemCount = x.OrderItems.Count,
                TotalAmount = x.TotalAmount,
                PaymentMethod = x.Payments
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => p.Method)
                    .FirstOrDefault() ?? PaymentMethods.Cash,
                StaffName = x.StaffUser != null ? x.StaffUser.FullName : null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return model;
    }

    public async Task<(bool Succeeded, string? Error, Guid? OrderId)> CheckoutAsync(
        PosCheckoutInputModel model,
        CancellationToken cancellationToken = default)
    {
        if (!model.StoreId.HasValue)
        {
            return (false, "Store is required.", null);
        }

        if (!await _storeAccess.CanAccessStoreAsync(model.StoreId.Value, cancellationToken))
        {
            return (false, "You do not have access to this store.", null);
        }

        if (!AllowedPaymentMethods.Contains(model.PaymentMethod))
        {
            return (false, "Payment method is invalid.", null);
        }
        var isHold = string.Equals(model.CheckoutMode, "hold", StringComparison.OrdinalIgnoreCase);

        var validItems = model.Items
            .Where(x => x.ProductId.HasValue && x.Quantity > 0)
            .GroupBy(x => x.ProductId!.Value)
            .Select(x => new PosCartItemInputModel
            {
                ProductId = x.Key,
                Quantity = x.Sum(i => i.Quantity)
            })
            .ToList();
        if (validItems.Count == 0)
        {
            return (false, "Cart is empty.", null);
        }

        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var storeId = model.StoreId.Value;
        var openShift = await _db.Shifts.FirstOrDefaultAsync(
            x => x.TenantId == tenantId
                && x.StoreId == storeId
                && x.OpenedBy == userId
                && x.Status == ShiftStatuses.Open,
            cancellationToken);
        if (openShift is null)
        {
            return (false, "You must open a shift before checkout.", null);
        }

        var productIds = validItems.Select(x => x.ProductId!.Value).ToArray();
        var saleProducts = await _db.StoreProducts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.StoreId == storeId
                && productIds.Contains(x.ProductId)
                && x.IsAvailable
                && !x.Product.IsDeleted
                && x.Product.IsActive)
            .Select(x => new SaleProductSnapshot(
                x.ProductId,
                x.Product.Name,
                x.Product.Sku,
                x.SellingPrice ?? x.Product.Price))
            .ToListAsync(cancellationToken);
        if (saleProducts.Count != validItems.Count)
        {
            return (false, "One or more products are not available at this store.", null);
        }

        var inventoryRows = await _db.Inventories
            .Where(x => x.TenantId == tenantId && x.StoreId == storeId && productIds.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);
        foreach (var item in validItems)
        {
            if (!inventoryRows.TryGetValue(item.ProductId!.Value, out var inventory) ||
                inventory.Quantity < item.Quantity)
            {
                var productName = saleProducts.First(x => x.ProductId == item.ProductId.Value).Name;
                return (false, $"Not enough stock for {productName}.", null);
            }
        }

        var productMap = saleProducts.ToDictionary(x => x.ProductId);
        var subTotal = validItems.Sum(x => productMap[x.ProductId!.Value].Price * x.Quantity);
        if (model.DiscountAmount < 0 || model.DiscountAmount > subTotal)
        {
            return (false, "Discount amount is invalid.", null);
        }

        var taxAmount = 0m;
        var totalAmount = subTotal - model.DiscountAmount + taxAmount;
        if (!isHold &&
            string.Equals(model.PaymentMethod, PaymentMethods.Cash, StringComparison.OrdinalIgnoreCase) &&
            model.CustomerPaidAmount < totalAmount)
        {
            return (false, "Customer paid amount is not enough.", null);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StoreId = storeId,
            OrderCode = await GenerateOrderCodeAsync(tenantId, cancellationToken),
            StaffUserId = userId,
            ShiftId = openShift.Id,
            SubTotal = subTotal,
            DiscountAmount = model.DiscountAmount,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            PaymentStatus = isHold ? OrderPaymentStatuses.Unpaid : OrderPaymentStatuses.Paid,
            OrderStatus = isHold ? OrderStatuses.New : OrderStatuses.Completed,
            Note = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        _db.Orders.Add(order);

        foreach (var item in validItems)
        {
            var product = productMap[item.ProductId!.Value];
            var lineTotal = product.Price * item.Quantity;
            _db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = order.Id,
                ProductId = product.ProductId,
                ProductName = product.Name,
                Sku = product.Sku,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                DiscountAmount = 0m,
                LineTotal = lineTotal
            });

            if (!isHold)
            {
                var inventory = inventoryRows[product.ProductId];
                var before = inventory.Quantity;
                inventory.Quantity -= item.Quantity;
                inventory.UpdatedAt = DateTime.UtcNow;
                inventory.UpdatedBy = userId;

                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    StoreId = storeId,
                    ProductId = product.ProductId,
                    Type = InventoryTransactionTypes.Sale,
                    Quantity = item.Quantity,
                    BeforeQuantity = before,
                    AfterQuantity = inventory.Quantity,
                    Reason = $"POS sale {order.OrderCode}",
                    ReferenceType = nameof(Order),
                    ReferenceId = order.Id.ToString(),
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = order.Id,
            Method = model.PaymentMethod,
            Amount = totalAmount,
            TransactionCode = string.IsNullOrWhiteSpace(model.TransactionCode) ? null : model.TransactionCode.Trim(),
            PaidAt = isHold ? null : DateTime.UtcNow,
            Status = isHold ? PaymentStatuses.Pending : PaymentStatuses.Paid,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            isHold ? "HoldOrder" : "CreateOrder",
            nameof(Order),
            order.Id.ToString(),
            newValue: $"OrderCode={order.OrderCode}; Total={order.TotalAmount:#,##0.##}; Items={validItems.Count}",
            tenantId: tenantId,
            storeId: storeId,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (isHold)
        {
            await NotifyOrderRealtimeAsync(tenantId, storeId, userId, order, validItems.Count, cancellationToken);
        }
        else
        {
            await NotifyCheckoutRealtimeAsync(
                tenantId,
                storeId,
                userId,
                order,
                validItems,
                productMap,
                inventoryRows,
                cancellationToken);
        }

        return (true, null, order.Id);
    }

    public async Task<(bool Succeeded, string? Error, Guid? OrderId)> CompletePendingOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var order = await _db.Orders
            .Include(x => x.OrderItems)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (false, "Pending order not found.", null);
        }

        if (!await _storeAccess.CanAccessStoreAsync(order.StoreId, cancellationToken))
        {
            return (false, "You do not have access to this order.", null);
        }

        if (order.OrderStatus != OrderStatuses.New || order.PaymentStatus != OrderPaymentStatuses.Unpaid)
        {
            return (false, "Only unpaid queued orders can be completed.", null);
        }

        var openShift = await _db.Shifts.FirstOrDefaultAsync(
            x => x.TenantId == tenantId
                && x.StoreId == order.StoreId
                && x.OpenedBy == userId
                && x.Status == ShiftStatuses.Open,
            cancellationToken);
        if (openShift is null)
        {
            return (false, "You must open a shift before completing queued orders.", null);
        }

        var productIds = order.OrderItems.Select(x => x.ProductId).Distinct().ToArray();
        var saleProducts = await _db.StoreProducts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.StoreId == order.StoreId
                && productIds.Contains(x.ProductId)
                && x.IsAvailable
                && !x.Product.IsDeleted
                && x.Product.IsActive)
            .Select(x => new SaleProductSnapshot(
                x.ProductId,
                x.Product.Name,
                x.Product.Sku,
                x.SellingPrice ?? x.Product.Price))
            .ToListAsync(cancellationToken);
        if (saleProducts.Count != productIds.Length)
        {
            return (false, "One or more queued products are no longer available.", null);
        }

        var inventoryRows = await _db.Inventories
            .Where(x => x.TenantId == tenantId && x.StoreId == order.StoreId && productIds.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);
        foreach (var item in order.OrderItems)
        {
            if (!inventoryRows.TryGetValue(item.ProductId, out var inventory) || inventory.Quantity < item.Quantity)
            {
                return (false, $"Not enough stock for {item.ProductName}.", null);
            }
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var item in order.OrderItems)
        {
            var inventory = inventoryRows[item.ProductId];
            var before = inventory.Quantity;
            inventory.Quantity -= item.Quantity;
            inventory.UpdatedAt = DateTime.UtcNow;
            inventory.UpdatedBy = userId;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StoreId = order.StoreId,
                ProductId = item.ProductId,
                Type = InventoryTransactionTypes.Sale,
                Quantity = item.Quantity,
                BeforeQuantity = before,
                AfterQuantity = inventory.Quantity,
                Reason = $"POS queued sale {order.OrderCode}",
                ReferenceType = nameof(Order),
                ReferenceId = order.Id.ToString(),
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
        }

        var oldValue = $"Status={order.OrderStatus}; PaymentStatus={order.PaymentStatus}";
        order.OrderStatus = OrderStatuses.Completed;
        order.PaymentStatus = OrderPaymentStatuses.Paid;
        order.StaffUserId = userId;
        order.ShiftId = openShift.Id;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = userId;

        var payment = order.Payments.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        if (payment is null)
        {
            _db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderId = order.Id,
                Method = PaymentMethods.Cash,
                Amount = order.TotalAmount,
                PaidAt = DateTime.UtcNow,
                Status = PaymentStatuses.Paid,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            payment.Status = PaymentStatuses.Paid;
            payment.PaidAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "CompletePendingOrder",
            nameof(Order),
            order.Id.ToString(),
            oldValue,
            $"Status={order.OrderStatus}; PaymentStatus={order.PaymentStatus}",
            tenantId,
            order.StoreId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var productMap = saleProducts.ToDictionary(x => x.ProductId);
        var validItems = order.OrderItems
            .Select(x => new PosCartItemInputModel { ProductId = x.ProductId, Quantity = x.Quantity })
            .ToList();
        await NotifyCheckoutRealtimeAsync(
            tenantId,
            order.StoreId,
            userId,
            order,
            validItems,
            productMap,
            inventoryRows,
            cancellationToken);

        return (true, null, order.Id);
    }

    public async Task<(bool Succeeded, string? Error)> CancelPendingOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var order = await _db.Orders
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return (false, "Pending order not found.");
        }

        if (!await _storeAccess.CanAccessStoreAsync(order.StoreId, cancellationToken))
        {
            return (false, "You do not have access to this order.");
        }

        if (order.OrderStatus != OrderStatuses.New || order.PaymentStatus != OrderPaymentStatuses.Unpaid)
        {
            return (false, "Only unpaid queued orders can be cancelled.");
        }

        var oldValue = $"Status={order.OrderStatus}; PaymentStatus={order.PaymentStatus}";
        order.OrderStatus = OrderStatuses.Cancelled;
        order.PaymentStatus = OrderPaymentStatuses.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancelledBy = userId;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = userId;
        foreach (var payment in order.Payments)
        {
            payment.Status = PaymentStatuses.Cancelled;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "CancelPendingOrder",
            nameof(Order),
            order.Id.ToString(),
            oldValue,
            $"Status={order.OrderStatus}; PaymentStatus={order.PaymentStatus}",
            tenantId,
            order.StoreId,
            cancellationToken);
        await _realtimeNotifier.OrderCancelledAsync(
            new OrderCancelledEvent(
                tenantId,
                order.StoreId,
                order.Id,
                order.OrderCode,
                order.PaymentStatus,
                order.OrderStatus,
                order.CancelledAt ?? DateTime.UtcNow),
            cancellationToken);

        return (true, null);
    }

    private async Task NotifyCheckoutRealtimeAsync(
        Guid tenantId,
        Guid storeId,
        string userId,
        Order order,
        IReadOnlyList<PosCartItemInputModel> validItems,
        IReadOnlyDictionary<Guid, SaleProductSnapshot> productMap,
        IReadOnlyDictionary<Guid, Models.Inventory> inventoryRows,
        CancellationToken cancellationToken)
    {
        await NotifyOrderRealtimeAsync(
            tenantId,
            storeId,
            userId,
            order,
            validItems.Count,
            cancellationToken);

        var store = await _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == storeId)
            .Select(x => new { x.Name, x.Code })
            .FirstAsync(cancellationToken);
        foreach (var item in validItems)
        {
            var productId = item.ProductId!.Value;
            var product = productMap[productId];
            var inventory = inventoryRows[productId];
            await _realtimeNotifier.InventoryChangedAsync(
                new InventoryChangedEvent(
                    tenantId,
                    storeId,
                    productId,
                    store.Name,
                    store.Code,
                    product.Name,
                    product.Sku,
                    inventory.Quantity,
                    inventory.MinQuantity,
                    InventoryTransactionTypes.Sale,
                    -item.Quantity,
                    DateTime.UtcNow),
                cancellationToken);
        }
    }

    private async Task NotifyOrderRealtimeAsync(
        Guid tenantId,
        Guid storeId,
        string userId,
        Order order,
        int itemCount,
        CancellationToken cancellationToken)
    {
        var store = await _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == storeId)
            .Select(x => new { x.Name, x.Code })
            .FirstAsync(cancellationToken);
        var staffName = await _db.AspNetUsers
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => x.FullName ?? x.UserName ?? x.Email)
            .FirstOrDefaultAsync(cancellationToken);

        await _realtimeNotifier.OrderCreatedAsync(
            new OrderCreatedEvent(
                tenantId,
                storeId,
                order.Id,
                order.OrderCode,
                store.Name,
                store.Code,
                staffName,
                itemCount,
                order.TotalAmount,
                order.PaymentStatus,
                order.OrderStatus,
                order.CreatedAt),
            cancellationToken);
    }

    private sealed record SaleProductSnapshot(Guid ProductId, string Name, string? Sku, decimal Price);

    private async Task<string> GenerateOrderCodeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = $"POS-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Random.Shared.Next(1000, 9999)}";
            var exists = await _db.Orders.AnyAsync(x => x.TenantId == tenantId && x.OrderCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }

        return $"POS-{Guid.NewGuid():N}"[..30];
    }

    private async Task<IReadOnlyList<StoreOptionViewModel>> GetStoreOptionsAsync(
        IReadOnlyCollection<Guid> accessibleStoreIds,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        return await _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && accessibleStoreIds.Contains(x.Id) && !x.IsDeleted && x.Status == StoreStatuses.Active)
            .OrderBy(x => x.Name)
            .Select(x => new StoreOptionViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code
            })
            .ToListAsync(cancellationToken);
    }

    private Guid RequireTenantId()
    {
        if (!_currentUser.TenantId.HasValue)
        {
            throw new InvalidOperationException("Current user does not have a tenant.");
        }

        return _currentUser.TenantId.Value;
    }

    private string RequireUserId()
    {
        if (string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            throw new InvalidOperationException("Current user is not authenticated.");
        }

        return _currentUser.UserId;
    }
}
