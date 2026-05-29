using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.ViewModels.Owner.StoreProducts;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Owner;

public sealed class OwnerStoreProductService : IOwnerStoreProductService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public OwnerStoreProductService(
        StoreFlowDbContext db,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<StoreProductIndexViewModel> GetStoreProductsAsync(
        Guid? storeId,
        string? search,
        string? availability,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var query = _db.StoreProducts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.Store.IsDeleted && !x.Product.IsDeleted);

        if (storeId.HasValue)
        {
            query = query.Where(x => x.StoreId == storeId.Value);
        }

        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(x =>
                x.Product.Name.Contains(trimmedSearch) ||
                x.Store.Name.Contains(trimmedSearch) ||
                x.Store.Code.Contains(trimmedSearch) ||
                (x.Product.Sku != null && x.Product.Sku.Contains(trimmedSearch)) ||
                (x.Product.Barcode != null && x.Product.Barcode.Contains(trimmedSearch)));
        }

        if (string.Equals(availability, "available", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsAvailable);
        }
        else if (string.Equals(availability, "unavailable", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsAvailable);
        }

        var storeProducts = await query
            .OrderBy(x => x.Store.Name)
            .ThenBy(x => x.Product.Name)
            .Select(x => new StoreProductListItemViewModel
            {
                Id = x.Id,
                StoreId = x.StoreId,
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Sku = x.Product.Sku,
                CategoryName = x.Product.Category != null ? x.Product.Category.Name : null,
                BasePrice = x.Product.Price,
                SellingPrice = x.SellingPrice,
                IsAvailable = x.IsAvailable,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var baseQuery = _db.StoreProducts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.Store.IsDeleted && !x.Product.IsDeleted);

        return new StoreProductIndexViewModel
        {
            StoreId = storeId,
            Search = trimmedSearch,
            Availability = availability,
            TotalAssignments = await baseQuery.CountAsync(cancellationToken),
            AvailableAssignments = await baseQuery.CountAsync(x => x.IsAvailable, cancellationToken),
            UnavailableAssignments = await baseQuery.CountAsync(x => !x.IsAvailable, cancellationToken),
            StoreCount = await _db.Stores.CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken),
            ProductCount = await _db.Products.CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken),
            Stores = await GetStoreOptionsAsync(tenantId, cancellationToken),
            StoreProducts = storeProducts
        };
    }

    public async Task<StoreProductAssignViewModel> GetAssignFormAsync(
        StoreProductAssignViewModel? model = null,
        CancellationToken cancellationToken = default)
    {
        model ??= new StoreProductAssignViewModel();
        var tenantId = RequireTenantId();
        model.Stores = await GetStoreOptionsAsync(tenantId, cancellationToken);
        model.Products = await GetProductOptionsAsync(tenantId, cancellationToken);
        return model;
    }

    public async Task<StoreProductEditViewModel?> GetEditFormAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        return await _db.StoreProducts
            .AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId && !x.Store.IsDeleted && !x.Product.IsDeleted)
            .Select(x => new StoreProductEditViewModel
            {
                Id = x.Id,
                StoreId = x.StoreId,
                ProductId = x.ProductId,
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                ProductName = x.Product.Name,
                Sku = x.Product.Sku,
                BasePrice = x.Product.Price,
                SellingPrice = x.SellingPrice,
                IsAvailable = x.IsAvailable
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error, Guid? StoreProductId)> AssignProductAsync(
        StoreProductAssignViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        if (!model.StoreId.HasValue || !model.ProductId.HasValue)
        {
            return (false, "Store and product are required.", null);
        }

        var storeExists = await _db.Stores.AnyAsync(
            x => x.Id == model.StoreId.Value && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (!storeExists)
        {
            return (false, "Store not found in this tenant.", null);
        }

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == model.ProductId.Value && x.TenantId == tenantId && !x.IsDeleted && x.IsActive,
                cancellationToken);
        if (product is null)
        {
            return (false, "Active product not found in this tenant.", null);
        }

        if (model.SellingPrice < 0)
        {
            return (false, "Selling price must be greater than or equal to 0.", null);
        }

        var existing = await _db.StoreProducts.FirstOrDefaultAsync(
            x => x.TenantId == tenantId
                && x.StoreId == model.StoreId.Value
                && x.ProductId == model.ProductId.Value,
            cancellationToken);

        var normalizedPrice = NormalizePrice(model.SellingPrice);
        if (existing is not null)
        {
            var oldValue = $"SellingPrice={existing.SellingPrice}; Available={existing.IsAvailable}";
            existing.SellingPrice = normalizedPrice;
            existing.IsAvailable = model.IsAvailable;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = _currentUser.UserId;
            await _db.SaveChangesAsync(cancellationToken);

            await _auditLog.LogAsync(
                "UpdateStoreProduct",
                nameof(StoreProduct),
                existing.Id.ToString(),
                oldValue,
                $"SellingPrice={existing.SellingPrice}; Available={existing.IsAvailable}",
                tenantId,
                existing.StoreId,
                cancellationToken);

            return (true, null, existing.Id);
        }

        var storeProduct = new StoreProduct
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StoreId = model.StoreId.Value,
            ProductId = model.ProductId.Value,
            SellingPrice = normalizedPrice,
            IsAvailable = model.IsAvailable,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.StoreProducts.Add(storeProduct);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "AssignStoreProduct",
            nameof(StoreProduct),
            storeProduct.Id.ToString(),
            newValue: $"StoreId={storeProduct.StoreId}; Product={product.Name}; SellingPrice={storeProduct.SellingPrice}; Available={storeProduct.IsAvailable}",
            tenantId: tenantId,
            storeId: storeProduct.StoreId,
            cancellationToken: cancellationToken);

        return (true, null, storeProduct.Id);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateStoreProductAsync(
        Guid id,
        StoreProductEditViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var storeProduct = await _db.StoreProducts.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.Store.IsDeleted && !x.Product.IsDeleted,
            cancellationToken);
        if (storeProduct is null)
        {
            return (false, "Store product assignment not found.");
        }

        if (model.SellingPrice < 0)
        {
            return (false, "Selling price must be greater than or equal to 0.");
        }

        var oldValue = $"SellingPrice={storeProduct.SellingPrice}; Available={storeProduct.IsAvailable}";
        storeProduct.SellingPrice = NormalizePrice(model.SellingPrice);
        storeProduct.IsAvailable = model.IsAvailable;
        storeProduct.UpdatedAt = DateTime.UtcNow;
        storeProduct.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "UpdateStoreProduct",
            nameof(StoreProduct),
            storeProduct.Id.ToString(),
            oldValue,
            $"SellingPrice={storeProduct.SellingPrice}; Available={storeProduct.IsAvailable}",
            tenantId,
            storeProduct.StoreId,
            cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SetAvailabilityAsync(
        Guid id,
        bool isAvailable,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var storeProduct = await _db.StoreProducts.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.Store.IsDeleted && !x.Product.IsDeleted,
            cancellationToken);
        if (storeProduct is null)
        {
            return (false, "Store product assignment not found.");
        }

        var oldValue = storeProduct.IsAvailable.ToString();
        storeProduct.IsAvailable = isAvailable;
        storeProduct.UpdatedAt = DateTime.UtcNow;
        storeProduct.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            isAvailable ? "EnableStoreProduct" : "DisableStoreProduct",
            nameof(StoreProduct),
            storeProduct.Id.ToString(),
            oldValue,
            isAvailable.ToString(),
            tenantId,
            storeProduct.StoreId,
            cancellationToken);

        return (true, null);
    }

    public async Task<decimal?> GetEffectiveSellingPriceAsync(
        Guid storeId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        return await _db.StoreProducts
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.StoreId == storeId
                && x.ProductId == productId
                && x.IsAvailable
                && !x.Store.IsDeleted
                && !x.Product.IsDeleted
                && x.Product.IsActive)
            .Select(x => (decimal?)(x.SellingPrice ?? x.Product.Price))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<StoreProductStoreOptionViewModel>> GetStoreOptionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new StoreProductStoreOptionViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<StoreProductProductOptionViewModel>> GetProductOptionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _db.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new StoreProductProductOptionViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Sku = x.Sku,
                Price = x.Price
            })
            .ToListAsync(cancellationToken);
    }

    private Guid RequireTenantId()
    {
        if (!_currentUser.TenantId.HasValue)
        {
            throw new InvalidOperationException("Current owner does not have a tenant.");
        }

        return _currentUser.TenantId.Value;
    }

    private static decimal? NormalizePrice(decimal? value)
        => value.HasValue ? decimal.Round(value.Value, 2) : null;
}
