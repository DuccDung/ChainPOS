using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.ViewModels.Owner.Stores;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Owner;

public sealed class OwnerStoreService : IOwnerStoreService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        StoreStatuses.Active,
        StoreStatuses.Inactive,
        StoreStatuses.Closed
    };

    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public OwnerStoreService(
        StoreFlowDbContext db,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<StoreIndexViewModel> GetStoresAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var query = _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(x =>
                x.Name.Contains(trimmedSearch) ||
                x.Code.Contains(trimmedSearch) ||
                (x.Address != null && x.Address.Contains(trimmedSearch)) ||
                (x.Phone != null && x.Phone.Contains(trimmedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var stores = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new StoreListItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Address = x.Address,
                Phone = x.Phone,
                Status = x.Status,
                StaffCount = x.UserStores.Count(s => s.IsActive),
                ProductCount = x.StoreProducts.Count(p => p.IsAvailable),
                OrderCount = x.Orders.Count,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var baseQuery = _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        return new StoreIndexViewModel
        {
            Search = trimmedSearch,
            Status = status,
            TotalStores = await baseQuery.CountAsync(cancellationToken),
            ActiveStores = await baseQuery.CountAsync(x => x.Status == StoreStatuses.Active, cancellationToken),
            InactiveStores = await baseQuery.CountAsync(x => x.Status == StoreStatuses.Inactive, cancellationToken),
            ClosedStores = await baseQuery.CountAsync(x => x.Status == StoreStatuses.Closed, cancellationToken),
            MaxStores = await GetMaxStoresAsync(tenantId, cancellationToken),
            Stores = stores
        };
    }

    public async Task<StoreDetailsViewModel?> GetStoreDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var store = await _db.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        if (store is null)
        {
            return null;
        }

        return new StoreDetailsViewModel
        {
            Id = store.Id,
            Name = store.Name,
            Code = store.Code,
            Address = store.Address,
            Phone = store.Phone,
            Status = store.Status,
            CreatedAt = store.CreatedAt,
            UpdatedAt = store.UpdatedAt,
            StaffCount = await _db.UserStores.CountAsync(
                x => x.TenantId == tenantId && x.StoreId == id && x.IsActive,
                cancellationToken),
            ProductCount = await _db.StoreProducts.CountAsync(
                x => x.TenantId == tenantId && x.StoreId == id && x.IsAvailable,
                cancellationToken),
            InventoryItemCount = await _db.Inventories.CountAsync(
                x => x.TenantId == tenantId && x.StoreId == id,
                cancellationToken),
            LowStockCount = await _db.Inventories.CountAsync(
                x => x.TenantId == tenantId && x.StoreId == id && x.Quantity <= x.MinQuantity,
                cancellationToken),
            OrderCount = await _db.Orders.CountAsync(
                x => x.TenantId == tenantId && x.StoreId == id,
                cancellationToken),
            RevenueTotal = await _db.Orders
                .Where(x => x.TenantId == tenantId && x.StoreId == id && x.OrderStatus != OrderStatuses.Cancelled)
                .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m
        };
    }

    public async Task<StoreFormViewModel?> GetStoreFormAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        return await _db.Stores
            .AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new StoreFormViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Address = x.Address,
                Phone = x.Phone,
                Status = x.Status
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error, Guid? StoreId)> CreateStoreAsync(
        StoreFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var status = NormalizeStatus(model.Status);
        if (status is null)
        {
            return (false, "Invalid store status.", null);
        }

        var code = NormalizeCode(model.Code);
        var codeExists = await _db.Stores.AnyAsync(
            x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted,
            cancellationToken);
        if (codeExists)
        {
            return (false, "Store code already exists in this tenant.", null);
        }

        var maxStores = await GetMaxStoresAsync(tenantId, cancellationToken);
        if (maxStores.HasValue)
        {
            var currentStoreCount = await _db.Stores.CountAsync(
                x => x.TenantId == tenantId && !x.IsDeleted,
                cancellationToken);
            if (currentStoreCount >= maxStores.Value)
            {
                return (false, $"Store limit reached for current subscription plan ({maxStores.Value}).", null);
            }
        }

        var now = DateTime.UtcNow;
        var store = new Store
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = model.Name.Trim(),
            Code = code,
            Address = TrimToNull(model.Address),
            Phone = TrimToNull(model.Phone),
            Status = status,
            CreatedAt = now,
            CreatedBy = _currentUser.UserId
        };

        _db.Stores.Add(store);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "CreateStore",
            nameof(Store),
            store.Id.ToString(),
            newValue: $"Name={store.Name}; Code={store.Code}; Status={store.Status}",
            tenantId: tenantId,
            storeId: store.Id,
            cancellationToken: cancellationToken);

        return (true, null, store.Id);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateStoreAsync(
        Guid id,
        StoreFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var status = NormalizeStatus(model.Status);
        if (status is null)
        {
            return (false, "Invalid store status.");
        }

        var store = await _db.Stores.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (store is null)
        {
            return (false, "Store not found.");
        }

        var code = NormalizeCode(model.Code);
        var codeExists = await _db.Stores.AnyAsync(
            x => x.TenantId == tenantId && x.Id != id && x.Code == code && !x.IsDeleted,
            cancellationToken);
        if (codeExists)
        {
            return (false, "Store code already exists in this tenant.");
        }

        var oldValue = $"Name={store.Name}; Code={store.Code}; Status={store.Status}";
        store.Name = model.Name.Trim();
        store.Code = code;
        store.Address = TrimToNull(model.Address);
        store.Phone = TrimToNull(model.Phone);
        store.Status = status;
        store.UpdatedAt = DateTime.UtcNow;
        store.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "UpdateStore",
            nameof(Store),
            store.Id.ToString(),
            oldValue,
            $"Name={store.Name}; Code={store.Code}; Status={store.Status}",
            tenantId,
            store.Id,
            cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SetStoreStatusAsync(
        Guid id,
        string status,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var normalizedStatus = NormalizeStatus(status);
        if (normalizedStatus is null)
        {
            return (false, "Invalid store status.");
        }

        var store = await _db.Stores.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (store is null)
        {
            return (false, "Store not found.");
        }

        var oldStatus = store.Status;
        store.Status = normalizedStatus;
        store.UpdatedAt = DateTime.UtcNow;
        store.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "ChangeStoreStatus",
            nameof(Store),
            store.Id.ToString(),
            oldStatus,
            normalizedStatus,
            tenantId,
            store.Id,
            cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> DeleteStoreAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var store = await _db.Stores.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (store is null)
        {
            return (false, "Store not found.");
        }

        store.IsDeleted = true;
        store.Status = StoreStatuses.Closed;
        store.UpdatedAt = DateTime.UtcNow;
        store.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "DeleteStore",
            nameof(Store),
            store.Id.ToString(),
            newValue: $"SoftDeleted=True; Status={store.Status}",
            tenantId: tenantId,
            storeId: store.Id,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    private Guid RequireTenantId()
    {
        if (!_currentUser.TenantId.HasValue)
        {
            throw new InvalidOperationException("Current owner does not have a tenant.");
        }

        return _currentUser.TenantId.Value;
    }

    private async Task<int?> GetMaxStoresAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _db.TenantSubscriptions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.Status == "Active"
                && (x.EndDate == null || x.EndDate >= today)
                && x.Plan.IsActive
                && !x.Plan.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Plan.MaxStores)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var value = status.Trim();
        return ValidStatuses.TryGetValue(value, out var validStatus) ? validStatus : null;
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
