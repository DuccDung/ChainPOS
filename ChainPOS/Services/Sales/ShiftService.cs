using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.Services.Realtime;
using ChainPOS.Services.Security;
using ChainPOS.ViewModels.Sales;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Sales;

public sealed class ShiftService : IShiftService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IStoreAccessService _storeAccess;
    private readonly IAuditLogService _auditLog;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public ShiftService(
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

    public async Task<ShiftIndexViewModel> GetShiftsAsync(
        string areaName,
        Guid? storeId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var accessibleStoreIds = await _storeAccess.GetAccessibleStoreIdsAsync(cancellationToken);
        var query = _db.Shifts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && accessibleStoreIds.Contains(x.StoreId));

        if (storeId.HasValue)
        {
            query = query.Where(x => x.StoreId == storeId.Value);
        }

        if (string.Equals(status, ShiftStatuses.Open, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Status == ShiftStatuses.Open);
        }
        else if (string.Equals(status, ShiftStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Status == ShiftStatuses.Closed);
        }

        var userId = _currentUser.UserId;
        var isOwner = _currentUser.IsInRole(AppRoles.Owner);
        var shifts = await query
            .OrderByDescending(x => x.OpenedAt)
            .Select(x => new ShiftListItemViewModel
            {
                Id = x.Id,
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                OpenedByName = x.OpenedByNavigation.FullName ?? x.OpenedByNavigation.UserName ?? x.OpenedBy,
                OpenedAt = x.OpenedAt,
                ClosedAt = x.ClosedAt,
                OpeningCash = x.OpeningCash,
                ClosingCash = x.ClosingCash,
                ExpectedCash = x.ExpectedCash,
                DifferenceAmount = x.DifferenceAmount,
                Status = x.Status,
                OrderCount = x.Orders.Count(o => o.OrderStatus != OrderStatuses.Cancelled),
                RevenueTotal = x.Orders
                    .Where(o => o.OrderStatus != OrderStatuses.Cancelled)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                CanClose = x.Status == ShiftStatuses.Open
                    && (x.OpenedBy == userId || isOwner)
            })
            .ToListAsync(cancellationToken);

        var baseQuery = _db.Shifts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && accessibleStoreIds.Contains(x.StoreId));
        var today = DateTime.UtcNow.Date;

        return new ShiftIndexViewModel
        {
            AreaName = areaName,
            StoreId = storeId,
            Status = status,
            TotalShifts = await baseQuery.CountAsync(cancellationToken),
            OpenShifts = await baseQuery.CountAsync(x => x.Status == ShiftStatuses.Open, cancellationToken),
            ClosedShifts = await baseQuery.CountAsync(x => x.Status == ShiftStatuses.Closed, cancellationToken),
            CashExpectedToday = await baseQuery
                .Where(x => x.OpenedAt >= today)
                .SumAsync(x => (decimal?)x.ExpectedCash, cancellationToken) ?? 0m,
            Stores = await GetStoreOptionsAsync(accessibleStoreIds, cancellationToken),
            Shifts = shifts
        };
    }

    public async Task<ShiftOpenViewModel> GetOpenFormAsync(
        string areaName,
        ShiftOpenViewModel? model = null,
        CancellationToken cancellationToken = default)
    {
        model ??= new ShiftOpenViewModel();
        model.AreaName = areaName;
        var accessibleStoreIds = await _storeAccess.GetAccessibleStoreIdsAsync(cancellationToken);
        model.Stores = await GetStoreOptionsAsync(accessibleStoreIds, cancellationToken);
        return model;
    }

    public async Task<(bool Succeeded, string? Error, Guid? ShiftId)> OpenShiftAsync(
        ShiftOpenViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!model.StoreId.HasValue)
        {
            return (false, "Store is required.", null);
        }

        if (model.OpeningCash < 0)
        {
            return (false, "Opening cash must be greater than or equal to 0.", null);
        }

        if (!await _storeAccess.CanAccessStoreAsync(model.StoreId.Value, cancellationToken))
        {
            return (false, "You do not have access to this store.", null);
        }

        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var hasOpenShift = await _db.Shifts.AnyAsync(
            x => x.TenantId == tenantId && x.OpenedBy == userId && x.Status == ShiftStatuses.Open,
            cancellationToken);
        if (hasOpenShift)
        {
            return (false, "You already have an open shift.", null);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StoreId = model.StoreId.Value,
            OpenedBy = userId,
            OpenedAt = DateTime.UtcNow,
            OpeningCash = model.OpeningCash,
            Status = ShiftStatuses.Open
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "OpenShift",
            nameof(Shift),
            shift.Id.ToString(),
            newValue: $"OpeningCash={shift.OpeningCash:#,##0.##}",
            tenantId: tenantId,
            storeId: shift.StoreId,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyShiftChangedAsync(shift, cancellationToken);

        return (true, null, shift.Id);
    }

    public async Task<ShiftCloseViewModel?> GetCloseFormAsync(
        string areaName,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var shift = await _db.Shifts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.StoreId,
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                x.OpenedAt,
                x.OpenedBy,
                x.OpeningCash,
                x.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (shift is null || !await CanCloseShiftAsync(shift.StoreId, shift.OpenedBy, cancellationToken))
        {
            return null;
        }

        var cashSales = await GetCashSalesAsync(id, cancellationToken);
        var expectedCash = shift.OpeningCash + cashSales;

        return new ShiftCloseViewModel
        {
            AreaName = areaName,
            Id = shift.Id,
            StoreName = shift.StoreName,
            StoreCode = shift.StoreCode,
            OpenedAt = shift.OpenedAt,
            OpeningCash = shift.OpeningCash,
            CashSales = cashSales,
            ExpectedCash = expectedCash,
            ClosingCash = expectedCash
        };
    }

    public async Task<(bool Succeeded, string? Error)> CloseShiftAsync(
        Guid id,
        ShiftCloseViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model.ClosingCash < 0)
        {
            return (false, "Closing cash must be greater than or equal to 0.");
        }

        var tenantId = RequireTenantId();
        var shift = await _db.Shifts.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == id,
            cancellationToken);
        if (shift is null)
        {
            return (false, "Shift not found.");
        }

        if (shift.Status != ShiftStatuses.Open)
        {
            return (false, "Only open shifts can be closed.");
        }

        if (!await CanCloseShiftAsync(shift.StoreId, shift.OpenedBy, cancellationToken))
        {
            return (false, "You do not have access to close this shift.");
        }

        var cashSales = await GetCashSalesAsync(id, cancellationToken);
        var expectedCash = shift.OpeningCash + cashSales;
        var oldValue = $"Status={shift.Status}; OpeningCash={shift.OpeningCash:#,##0.##}";

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        shift.ClosedBy = RequireUserId();
        shift.ClosedAt = DateTime.UtcNow;
        shift.ClosingCash = model.ClosingCash;
        shift.ExpectedCash = expectedCash;
        shift.DifferenceAmount = model.ClosingCash - expectedCash;
        shift.Status = ShiftStatuses.Closed;

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "CloseShift",
            nameof(Shift),
            shift.Id.ToString(),
            oldValue: oldValue,
            newValue: $"Status={shift.Status}; ClosingCash={shift.ClosingCash:#,##0.##}; ExpectedCash={shift.ExpectedCash:#,##0.##}; Difference={shift.DifferenceAmount:#,##0.##}",
            tenantId: tenantId,
            storeId: shift.StoreId,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyShiftChangedAsync(shift, cancellationToken);

        return (true, null);
    }

    private async Task<bool> CanCloseShiftAsync(Guid storeId, string openedBy, CancellationToken cancellationToken)
    {
        if (!await _storeAccess.CanAccessStoreAsync(storeId, cancellationToken))
        {
            return false;
        }

        return _currentUser.IsInRole(AppRoles.Owner) || openedBy == _currentUser.UserId;
    }

    private async Task<decimal> GetCashSalesAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        return await _db.Payments
            .AsNoTracking()
            .Where(x => x.Method == PaymentMethods.Cash
                && x.Status == PaymentStatuses.Paid
                && x.Order.ShiftId == shiftId
                && x.Order.OrderStatus != OrderStatuses.Cancelled)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
    }

    private async Task NotifyShiftChangedAsync(Shift shift, CancellationToken cancellationToken)
    {
        var store = await _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == shift.TenantId && x.Id == shift.StoreId)
            .Select(x => new { x.Name, x.Code })
            .FirstAsync(cancellationToken);
        var openedBy = await _db.AspNetUsers
            .AsNoTracking()
            .Where(x => x.Id == shift.OpenedBy)
            .Select(x => x.FullName ?? x.UserName ?? x.Email ?? x.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? shift.OpenedBy;

        await _realtimeNotifier.ShiftChangedAsync(
            new ShiftChangedEvent(
                shift.TenantId,
                shift.StoreId,
                shift.Id,
                store.Name,
                store.Code,
                openedBy,
                shift.Status,
                shift.OpeningCash,
                shift.ClosingCash,
                shift.ExpectedCash,
                shift.DifferenceAmount,
                DateTime.UtcNow),
            cancellationToken);
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
