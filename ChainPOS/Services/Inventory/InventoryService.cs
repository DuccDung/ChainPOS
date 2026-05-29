using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.Services.Realtime;
using ChainPOS.Services.Security;
using ChainPOS.ViewModels.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Inventory;

public sealed class InventoryService : IInventoryService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IStoreAccessService _storeAccess;
    private readonly IAuditLogService _auditLog;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public InventoryService(
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

    public async Task<InventoryIndexViewModel> GetInventoryAsync(
        string areaName,
        Guid? storeId,
        string? search,
        string? stockStatus,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var accessibleStoreIds = await _storeAccess.GetAccessibleStoreIdsAsync(cancellationToken);
        var query = _db.Inventories
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
                x.Product.Name.Contains(trimmedSearch) ||
                (x.Product.Sku != null && x.Product.Sku.Contains(trimmedSearch)) ||
                (x.Product.Barcode != null && x.Product.Barcode.Contains(trimmedSearch)) ||
                x.Store.Name.Contains(trimmedSearch) ||
                x.Store.Code.Contains(trimmedSearch));
        }

        if (string.Equals(stockStatus, "low", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Quantity > 0 && x.Quantity <= x.MinQuantity);
        }
        else if (string.Equals(stockStatus, "out", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Quantity <= 0);
        }
        else if (string.Equals(stockStatus, "ok", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.Quantity > x.MinQuantity);
        }

        var items = await query
            .OrderBy(x => x.Store.Name)
            .ThenBy(x => x.Product.Name)
            .Select(x => new InventoryListItemViewModel
            {
                Id = x.Id,
                StoreId = x.StoreId,
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Sku = x.Product.Sku,
                Barcode = x.Product.Barcode,
                CategoryName = x.Product.Category != null ? x.Product.Category.Name : null,
                Quantity = x.Quantity,
                MinQuantity = x.MinQuantity,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var baseQuery = _db.Inventories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && accessibleStoreIds.Contains(x.StoreId));

        return new InventoryIndexViewModel
        {
            AreaName = areaName,
            StoreId = storeId,
            Search = trimmedSearch,
            StockStatus = stockStatus,
            TotalItems = await baseQuery.CountAsync(cancellationToken),
            LowStockItems = await baseQuery.CountAsync(x => x.Quantity > 0 && x.Quantity <= x.MinQuantity, cancellationToken),
            OutOfStockItems = await baseQuery.CountAsync(x => x.Quantity <= 0, cancellationToken),
            TotalQuantity = await baseQuery.SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m,
            Stores = await GetStoreOptionsAsync(accessibleStoreIds, cancellationToken),
            Items = items
        };
    }

    public async Task<InventoryMovementViewModel> GetImportFormAsync(
        string areaName,
        InventoryMovementViewModel? model = null,
        CancellationToken cancellationToken = default)
    {
        model ??= new InventoryMovementViewModel { Quantity = 1, MinQuantity = 5 };
        model.AreaName = areaName;
        await PopulateMovementOptionsAsync(model, cancellationToken);
        await PopulateCurrentInventoryAsync(model, cancellationToken);
        return model;
    }

    public async Task<InventoryMovementViewModel> GetExportFormAsync(
        string areaName,
        InventoryMovementViewModel? model = null,
        CancellationToken cancellationToken = default)
    {
        model ??= new InventoryMovementViewModel { Quantity = 1 };
        model.AreaName = areaName;
        await PopulateMovementOptionsAsync(model, cancellationToken);
        await PopulateCurrentInventoryAsync(model, cancellationToken);
        return model;
    }

    public async Task<InventoryAdjustViewModel> GetAdjustFormAsync(
        string areaName,
        InventoryAdjustViewModel? model = null,
        CancellationToken cancellationToken = default)
    {
        model ??= new InventoryAdjustViewModel();
        model.AreaName = areaName;
        var accessibleStoreIds = await _storeAccess.GetAccessibleStoreIdsAsync(cancellationToken);
        model.Stores = await GetStoreOptionsAsync(accessibleStoreIds, cancellationToken);
        model.Products = await GetProductOptionsAsync(accessibleStoreIds, cancellationToken);
        if (model.StoreId.HasValue && model.ProductId.HasValue)
        {
            var tenantId = RequireTenantId();
            var inventory = await _db.Inventories
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.StoreId == model.StoreId.Value && x.ProductId == model.ProductId.Value,
                    cancellationToken);
            if (inventory is not null)
            {
                model.ActualQuantity = inventory.Quantity;
                model.MinQuantity = inventory.MinQuantity;
            }
        }
        return model;
    }

    public async Task<(bool Succeeded, string? Error)> ImportStockAsync(
        InventoryMovementViewModel model,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateMovementAsync(model.StoreId, model.ProductId, requireInventory: false, cancellationToken);
        if (!validation.Succeeded)
        {
            return (false, validation.Error);
        }

        if (model.Quantity <= 0)
        {
            return (false, "Quantity must be greater than 0.");
        }

        if (model.MinQuantity < 0)
        {
            return (false, "Min quantity must be greater than or equal to 0.");
        }

        var tenantId = RequireTenantId();
        var storeId = model.StoreId!.Value;
        var productId = model.ProductId!.Value;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var inventory = await _db.Inventories.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.StoreId == storeId && x.ProductId == productId,
            cancellationToken);
        if (inventory is null)
        {
            inventory = new Models.Inventory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StoreId = storeId,
                ProductId = productId,
                Quantity = 0m,
                MinQuantity = model.MinQuantity,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = _currentUser.UserId
            };
            _db.Inventories.Add(inventory);
        }

        var before = inventory.Quantity;
        inventory.Quantity += model.Quantity;
        inventory.MinQuantity = model.MinQuantity;
        inventory.UpdatedAt = DateTime.UtcNow;
        inventory.UpdatedBy = _currentUser.UserId;

        await AddTransactionAsync(
            tenantId,
            storeId,
            productId,
            InventoryTransactionTypes.Import,
            model.Quantity,
            before,
            inventory.Quantity,
            model.Reason,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "ImportStock",
            nameof(Models.Inventory),
            inventory.Id.ToString(),
            oldValue: before.ToString("#,##0.###"),
            newValue: inventory.Quantity.ToString("#,##0.###"),
            tenantId: tenantId,
            storeId: inventory.StoreId,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyInventoryChangedAsync(
            tenantId,
            storeId,
            productId,
            inventory.Quantity,
            inventory.MinQuantity,
            InventoryTransactionTypes.Import,
            model.Quantity,
            cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> ExportStockAsync(
        InventoryMovementViewModel model,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateMovementAsync(model.StoreId, model.ProductId, requireInventory: true, cancellationToken);
        if (!validation.Succeeded)
        {
            return (false, validation.Error);
        }

        if (model.Quantity <= 0)
        {
            return (false, "Quantity must be greater than 0.");
        }

        var tenantId = RequireTenantId();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var inventory = await _db.Inventories.FirstAsync(
            x => x.TenantId == tenantId && x.StoreId == model.StoreId!.Value && x.ProductId == model.ProductId!.Value,
            cancellationToken);
        if (inventory.Quantity < model.Quantity)
        {
            return (false, "Not enough stock for export.");
        }

        var before = inventory.Quantity;
        inventory.Quantity -= model.Quantity;
        inventory.UpdatedAt = DateTime.UtcNow;
        inventory.UpdatedBy = _currentUser.UserId;

        await AddTransactionAsync(
            tenantId,
            inventory.StoreId,
            inventory.ProductId,
            InventoryTransactionTypes.Export,
            model.Quantity,
            before,
            inventory.Quantity,
            model.Reason,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "ExportStock",
            nameof(Models.Inventory),
            inventory.Id.ToString(),
            oldValue: before.ToString("#,##0.###"),
            newValue: inventory.Quantity.ToString("#,##0.###"),
            tenantId: tenantId,
            storeId: inventory.StoreId,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyInventoryChangedAsync(
            tenantId,
            inventory.StoreId,
            inventory.ProductId,
            inventory.Quantity,
            inventory.MinQuantity,
            InventoryTransactionTypes.Export,
            -model.Quantity,
            cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> AdjustStockAsync(
        InventoryAdjustViewModel model,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateMovementAsync(model.StoreId, model.ProductId, requireInventory: true, cancellationToken);
        if (!validation.Succeeded)
        {
            return (false, validation.Error);
        }

        if (model.ActualQuantity < 0)
        {
            return (false, "Actual quantity must be greater than or equal to 0.");
        }

        if (model.MinQuantity < 0)
        {
            return (false, "Min quantity must be greater than or equal to 0.");
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            return (false, "Reason is required.");
        }

        var tenantId = RequireTenantId();
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var inventory = await _db.Inventories.FirstAsync(
            x => x.TenantId == tenantId && x.StoreId == model.StoreId!.Value && x.ProductId == model.ProductId!.Value,
            cancellationToken);

        var before = inventory.Quantity;
        var movementQuantity = Math.Abs(model.ActualQuantity - before);
        if (movementQuantity <= 0)
        {
            return (false, "Actual quantity must be different from current quantity.");
        }

        inventory.Quantity = model.ActualQuantity;
        inventory.MinQuantity = model.MinQuantity;
        inventory.UpdatedAt = DateTime.UtcNow;
        inventory.UpdatedBy = _currentUser.UserId;

        await AddTransactionAsync(
            tenantId,
            inventory.StoreId,
            inventory.ProductId,
            InventoryTransactionTypes.Adjust,
            movementQuantity,
            before,
            inventory.Quantity,
            model.Reason,
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "AdjustStock",
            nameof(Models.Inventory),
            inventory.Id.ToString(),
            oldValue: before.ToString("#,##0.###"),
            newValue: inventory.Quantity.ToString("#,##0.###"),
            tenantId: tenantId,
            storeId: inventory.StoreId,
            cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await NotifyInventoryChangedAsync(
            tenantId,
            inventory.StoreId,
            inventory.ProductId,
            inventory.Quantity,
            inventory.MinQuantity,
            InventoryTransactionTypes.Adjust,
            inventory.Quantity - before,
            cancellationToken);

        return (true, null);
    }

    private async Task<(bool Succeeded, string? Error)> ValidateMovementAsync(
        Guid? storeId,
        Guid? productId,
        bool requireInventory,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        if (!storeId.HasValue || !productId.HasValue)
        {
            return (false, "Store and product are required.");
        }

        if (!await _storeAccess.CanAccessStoreAsync(storeId.Value, cancellationToken))
        {
            return (false, "You do not have access to this store.");
        }

        var productAvailable = await _db.StoreProducts.AnyAsync(
            x => x.TenantId == tenantId
                && x.StoreId == storeId.Value
                && x.ProductId == productId.Value
                && x.IsAvailable
                && !x.Store.IsDeleted
                && x.Store.Status == StoreStatuses.Active
                && !x.Product.IsDeleted
                && x.Product.IsActive,
            cancellationToken);
        if (!productAvailable)
        {
            return (false, "Product is not available at this store.");
        }

        if (requireInventory)
        {
            var inventoryExists = await _db.Inventories.AnyAsync(
                x => x.TenantId == tenantId && x.StoreId == storeId.Value && x.ProductId == productId.Value,
                cancellationToken);
            if (!inventoryExists)
            {
                return (false, "Inventory item not found for this store and product.");
            }
        }

        return (true, null);
    }

    private async Task PopulateMovementOptionsAsync(
        InventoryMovementViewModel model,
        CancellationToken cancellationToken)
    {
        var accessibleStoreIds = await _storeAccess.GetAccessibleStoreIdsAsync(cancellationToken);
        model.Stores = await GetStoreOptionsAsync(accessibleStoreIds, cancellationToken);
        model.Products = await GetProductOptionsAsync(accessibleStoreIds, cancellationToken);
    }

    private async Task PopulateCurrentInventoryAsync(
        InventoryMovementViewModel model,
        CancellationToken cancellationToken)
    {
        if (!model.StoreId.HasValue || !model.ProductId.HasValue)
        {
            return;
        }

        var tenantId = RequireTenantId();
        var inventory = await _db.Inventories
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.StoreId == model.StoreId.Value && x.ProductId == model.ProductId.Value,
                cancellationToken);
        if (inventory is not null)
        {
            model.MinQuantity = inventory.MinQuantity;
        }
    }

    private async Task<IReadOnlyList<InventoryStoreOptionViewModel>> GetStoreOptionsAsync(
        IReadOnlyCollection<Guid> accessibleStoreIds,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        return await _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && accessibleStoreIds.Contains(x.Id) && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new InventoryStoreOptionViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<InventoryProductOptionViewModel>> GetProductOptionsAsync(
        IReadOnlyCollection<Guid> accessibleStoreIds,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        return await _db.StoreProducts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && accessibleStoreIds.Contains(x.StoreId)
                && x.IsAvailable
                && !x.Product.IsDeleted
                && x.Product.IsActive)
            .Select(x => new InventoryProductOptionViewModel
            {
                Id = x.ProductId,
                Name = x.Product.Name,
                Sku = x.Product.Sku
            })
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    private Task AddTransactionAsync(
        Guid tenantId,
        Guid storeId,
        Guid productId,
        string type,
        decimal quantity,
        decimal before,
        decimal after,
        string? reason,
        CancellationToken cancellationToken)
    {
        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StoreId = storeId,
            ProductId = productId,
            Type = type,
            Quantity = quantity,
            BeforeQuantity = before,
            AfterQuantity = after,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ReferenceType = "Manual",
            ReferenceId = null,
            CreatedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    private async Task NotifyInventoryChangedAsync(
        Guid tenantId,
        Guid storeId,
        Guid productId,
        decimal quantity,
        decimal minQuantity,
        string changeType,
        decimal delta,
        CancellationToken cancellationToken)
    {
        var row = await _db.StoreProducts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.StoreId == storeId && x.ProductId == productId)
            .Select(x => new
            {
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                ProductName = x.Product.Name,
                x.Product.Sku
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return;
        }

        await _realtimeNotifier.InventoryChangedAsync(
            new InventoryChangedEvent(
                tenantId,
                storeId,
                productId,
                row.StoreName,
                row.StoreCode,
                row.ProductName,
                row.Sku,
                quantity,
                minQuantity,
                changeType,
                delta,
                DateTime.UtcNow),
            cancellationToken);
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
