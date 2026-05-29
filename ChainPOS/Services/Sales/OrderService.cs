using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.Services.Realtime;
using ChainPOS.Services.Security;
using ChainPOS.ViewModels.Sales;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Sales;

public sealed class OrderService : IOrderService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IStoreAccessService _storeAccess;
    private readonly IAuditLogService _auditLog;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public OrderService(
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

    public async Task<OrderIndexViewModel> GetOrdersAsync(
        string areaName,
        Guid? storeId,
        string? search,
        string? status,
        string? paymentStatus,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var accessibleStoreIds = await _storeAccess.GetAccessibleStoreIdsAsync(cancellationToken);
        var query = _db.Orders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && accessibleStoreIds.Contains(x.StoreId));

        if (storeId.HasValue)
        {
            query = query.Where(x => x.StoreId == storeId.Value);
        }

        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(x =>
                x.OrderCode.Contains(trimmedSearch) ||
                x.Store.Name.Contains(trimmedSearch) ||
                (x.StaffUser != null && x.StaffUser.FullName != null && x.StaffUser.FullName.Contains(trimmedSearch)));
        }

        if (string.Equals(status, OrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.OrderStatus == OrderStatuses.Completed);
        }
        else if (string.Equals(status, OrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.OrderStatus == OrderStatuses.Cancelled);
        }
        else if (string.Equals(status, OrderStatuses.New, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.OrderStatus == OrderStatuses.New);
        }

        if (string.Equals(paymentStatus, OrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.PaymentStatus == OrderPaymentStatuses.Paid);
        }
        else if (string.Equals(paymentStatus, OrderPaymentStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.PaymentStatus == OrderPaymentStatuses.Cancelled);
        }
        else if (string.Equals(paymentStatus, OrderPaymentStatuses.Unpaid, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.PaymentStatus == OrderPaymentStatuses.Unpaid);
        }

        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);
            query = query.Where(x => x.CreatedAt >= start && x.CreatedAt < end);
        }

        var orders = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new OrderListItemViewModel
            {
                Id = x.Id,
                OrderCode = x.OrderCode,
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                StaffName = x.StaffUser != null ? x.StaffUser.FullName : null,
                ItemCount = x.OrderItems.Count,
                TotalAmount = x.TotalAmount,
                PaymentStatus = x.PaymentStatus,
                OrderStatus = x.OrderStatus,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var baseQuery = _db.Orders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && accessibleStoreIds.Contains(x.StoreId));

        return new OrderIndexViewModel
        {
            AreaName = areaName,
            StoreId = storeId,
            Search = trimmedSearch,
            Status = status,
            PaymentStatus = paymentStatus,
            Date = date,
            TotalOrders = await baseQuery.CountAsync(cancellationToken),
            CompletedOrders = await baseQuery.CountAsync(x => x.OrderStatus == OrderStatuses.Completed, cancellationToken),
            CancelledOrders = await baseQuery.CountAsync(x => x.OrderStatus == OrderStatuses.Cancelled, cancellationToken),
            RevenueTotal = await baseQuery
                .Where(x => x.OrderStatus != OrderStatuses.Cancelled)
                .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m,
            Stores = await GetStoreOptionsAsync(accessibleStoreIds, cancellationToken),
            Orders = orders
        };
    }

    public async Task<OrderDetailsViewModel?> GetOrderDetailsAsync(
        string areaName,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var order = await _db.Orders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new OrderDetailsViewModel
            {
                AreaName = areaName,
                Id = x.Id,
                OrderCode = x.OrderCode,
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                StaffName = x.StaffUser != null ? x.StaffUser.FullName : null,
                ShiftCode = x.Shift != null ? x.Shift.OpenedAt.ToString("yyyyMMdd-HHmm") : null,
                SubTotal = x.SubTotal,
                DiscountAmount = x.DiscountAmount,
                TaxAmount = x.TaxAmount,
                TotalAmount = x.TotalAmount,
                PaymentStatus = x.PaymentStatus,
                OrderStatus = x.OrderStatus,
                Note = x.Note,
                CreatedAt = x.CreatedAt,
                CancelledAt = x.CancelledAt,
                Items = x.OrderItems
                    .OrderBy(i => i.ProductName)
                    .Select(i => new OrderItemDetailsViewModel
                    {
                        ProductName = i.ProductName,
                        Sku = i.Sku,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        DiscountAmount = i.DiscountAmount,
                        LineTotal = i.LineTotal
                    })
                    .ToList(),
                Payments = x.Payments
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new PaymentDetailsViewModel
                    {
                        Method = p.Method,
                        Amount = p.Amount,
                        Status = p.Status,
                        TransactionCode = p.TransactionCode,
                        PaidAt = p.PaidAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (order is null || !await _storeAccess.CanAccessStoreAsync(await GetOrderStoreIdAsync(id, tenantId, cancellationToken), cancellationToken))
        {
            return null;
        }

        return order;
    }

    public async Task<(bool Succeeded, string? Error)> CancelOrderAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var order = await _db.Orders
            .Include(x => x.OrderItems)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (order is null)
        {
            return (false, "Order not found.");
        }

        if (!await _storeAccess.CanAccessStoreAsync(order.StoreId, cancellationToken))
        {
            return (false, "You do not have access to this order.");
        }

        if (order.OrderStatus == OrderStatuses.Cancelled)
        {
            return (false, "Order is already cancelled.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var store = await _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == order.StoreId)
            .Select(x => new { x.Name, x.Code })
            .FirstAsync(cancellationToken);
        var inventoryEvents = new List<InventoryChangedEvent>();
        foreach (var item in order.OrderItems)
        {
            var inventory = await _db.Inventories.FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.StoreId == order.StoreId && x.ProductId == item.ProductId,
                cancellationToken);
            if (inventory is null)
            {
                inventory = new Models.Inventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    StoreId = order.StoreId,
                    ProductId = item.ProductId,
                    Quantity = 0m,
                    MinQuantity = 0m,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId
                };
                _db.Inventories.Add(inventory);
            }

            var before = inventory.Quantity;
            inventory.Quantity += item.Quantity;
            inventory.UpdatedAt = DateTime.UtcNow;
            inventory.UpdatedBy = userId;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StoreId = order.StoreId,
                ProductId = item.ProductId,
                Type = InventoryTransactionTypes.Return,
                Quantity = item.Quantity,
                BeforeQuantity = before,
                AfterQuantity = inventory.Quantity,
                Reason = $"Cancel order {order.OrderCode}",
                ReferenceType = nameof(Order),
                ReferenceId = order.Id.ToString(),
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            });
            inventoryEvents.Add(new InventoryChangedEvent(
                tenantId,
                order.StoreId,
                item.ProductId,
                store.Name,
                store.Code,
                item.ProductName,
                item.Sku,
                inventory.Quantity,
                inventory.MinQuantity,
                InventoryTransactionTypes.Return,
                item.Quantity,
                DateTime.UtcNow));
        }

        var oldValue = $"Status={order.OrderStatus}; PaymentStatus={order.PaymentStatus}; Total={order.TotalAmount:#,##0.##}";
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
            "CancelOrder",
            nameof(Order),
            order.Id.ToString(),
            oldValue: oldValue,
            newValue: $"Status={order.OrderStatus}; PaymentStatus={order.PaymentStatus}",
            tenantId: tenantId,
            storeId: order.StoreId,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        foreach (var inventoryEvent in inventoryEvents)
        {
            await _realtimeNotifier.InventoryChangedAsync(inventoryEvent, cancellationToken);
        }

        return (true, null);
    }

    private async Task<Guid> GetOrderStoreIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        return await _db.Orders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => x.StoreId)
            .FirstOrDefaultAsync(cancellationToken);
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
