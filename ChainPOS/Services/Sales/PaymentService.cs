using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Common;
using ChainPOS.Services.Security;
using ChainPOS.ViewModels.Sales;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Sales;

public sealed class PaymentService : IPaymentService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IStoreAccessService _storeAccess;

    public PaymentService(
        StoreFlowDbContext db,
        ICurrentUserService currentUser,
        IStoreAccessService storeAccess)
    {
        _db = db;
        _currentUser = currentUser;
        _storeAccess = storeAccess;
    }

    public async Task<PaymentIndexViewModel> GetPaymentsAsync(
        string areaName,
        Guid? storeId,
        string? search,
        string? method,
        string? status,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var accessibleStoreIds = await _storeAccess.GetAccessibleStoreIdsAsync(cancellationToken);
        var baseQuery = _db.Payments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && accessibleStoreIds.Contains(x.Order.StoreId));

        var query = baseQuery;
        if (storeId.HasValue)
        {
            query = query.Where(x => x.Order.StoreId == storeId.Value);
        }

        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(x =>
                x.Order.OrderCode.Contains(trimmedSearch) ||
                (x.TransactionCode != null && x.TransactionCode.Contains(trimmedSearch)) ||
                x.Order.Store.Name.Contains(trimmedSearch) ||
                x.Order.Store.Code.Contains(trimmedSearch) ||
                (x.Order.StaffUser != null && x.Order.StaffUser.FullName != null && x.Order.StaffUser.FullName.Contains(trimmedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            query = query.Where(x => x.Method == method);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (date.HasValue)
        {
            var start = date.Value.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);
            query = query.Where(x => x.CreatedAt >= start && x.CreatedAt < end);
        }

        var payments = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PaymentListItemViewModel
            {
                Id = x.Id,
                OrderId = x.OrderId,
                OrderCode = x.Order.OrderCode,
                StoreName = x.Order.Store.Name,
                StoreCode = x.Order.Store.Code,
                StaffName = x.Order.StaffUser != null ? x.Order.StaffUser.FullName : null,
                Method = x.Method,
                Amount = x.Amount,
                TransactionCode = x.TransactionCode,
                Status = x.Status,
                OrderStatus = x.Order.OrderStatus,
                OrderPaymentStatus = x.Order.PaymentStatus,
                PaidAt = x.PaidAt,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PaymentIndexViewModel
        {
            AreaName = areaName,
            StoreId = storeId,
            Search = trimmedSearch,
            Method = method,
            Status = status,
            Date = date,
            TotalPayments = await baseQuery.CountAsync(cancellationToken),
            PaidPayments = await baseQuery.CountAsync(x => x.Status == PaymentStatuses.Paid, cancellationToken),
            PendingPayments = await baseQuery.CountAsync(x => x.Status == PaymentStatuses.Pending, cancellationToken),
            FailedPayments = await baseQuery.CountAsync(x => x.Status == PaymentStatuses.Failed, cancellationToken),
            PaidAmount = await baseQuery
                .Where(x => x.Status == PaymentStatuses.Paid && x.Order.OrderStatus != OrderStatuses.Cancelled)
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m,
            Stores = await GetStoreOptionsAsync(accessibleStoreIds, cancellationToken),
            Payments = payments
        };
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
}
