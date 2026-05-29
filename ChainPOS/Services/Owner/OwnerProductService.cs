using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.ViewModels.Owner.Products;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Owner;

public sealed class OwnerProductService : IOwnerProductService
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif"
    };

    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly IWebHostEnvironment _environment;

    public OwnerProductService(
        StoreFlowDbContext db,
        ICurrentUserService currentUser,
        IAuditLogService auditLog,
        IWebHostEnvironment environment)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _environment = environment;
    }

    public async Task<ProductIndexViewModel> GetProductsAsync(
        string? search,
        Guid? categoryId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var query = _db.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(x =>
                x.Name.Contains(trimmedSearch) ||
                (x.Sku != null && x.Sku.Contains(trimmedSearch)) ||
                (x.Barcode != null && x.Barcode.Contains(trimmedSearch)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
        }
        else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
        }

        var products = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProductListItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Sku = x.Sku,
                Barcode = x.Barcode,
                CategoryName = x.Category != null ? x.Category.Name : null,
                Price = x.Price,
                CostPrice = x.CostPrice,
                ImageUrl = x.ImageUrl,
                IsActive = x.IsActive,
                StoreCount = x.StoreProducts.Count(s => s.IsAvailable),
                InventoryItemCount = x.Inventories.Count,
                InventoryQuantity = x.Inventories.Sum(i => (decimal?)i.Quantity) ?? 0m,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var baseQuery = _db.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        return new ProductIndexViewModel
        {
            Search = trimmedSearch,
            CategoryId = categoryId,
            Status = status,
            TotalProducts = await baseQuery.CountAsync(cancellationToken),
            ActiveProducts = await baseQuery.CountAsync(x => x.IsActive, cancellationToken),
            InactiveProducts = await baseQuery.CountAsync(x => !x.IsActive, cancellationToken),
            CategoryCount = await _db.Categories.CountAsync(
                x => x.TenantId == tenantId && !x.IsDeleted,
                cancellationToken),
            MaxProducts = await GetMaxProductsAsync(tenantId, cancellationToken),
            Categories = await GetCategoryOptionsAsync(tenantId, cancellationToken),
            Products = products
        };
    }

    public async Task<ProductFormViewModel> GetCreateFormAsync(
        ProductFormViewModel? model = null,
        CancellationToken cancellationToken = default)
    {
        model ??= new ProductFormViewModel();
        model.Categories = await GetCategoryOptionsAsync(RequireTenantId(), cancellationToken);
        return model;
    }

    public async Task<ProductFormViewModel?> GetProductFormAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var model = await _db.Products
            .AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new ProductFormViewModel
            {
                Id = x.Id,
                Name = x.Name,
                CategoryId = x.CategoryId,
                Sku = x.Sku,
                Barcode = x.Barcode,
                Description = x.Description,
                Price = x.Price,
                CostPrice = x.CostPrice,
                IsActive = x.IsActive,
                ExistingImageUrl = x.ImageUrl
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (model is null)
        {
            return null;
        }

        model.Categories = await GetCategoryOptionsAsync(tenantId, cancellationToken);
        return model;
    }

    public async Task<ProductDetailsViewModel?> GetProductDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        return await _db.Products
            .AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new ProductDetailsViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Sku = x.Sku,
                Barcode = x.Barcode,
                CategoryName = x.Category != null ? x.Category.Name : null,
                Description = x.Description,
                Price = x.Price,
                CostPrice = x.CostPrice,
                ImageUrl = x.ImageUrl,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                StoreCount = x.StoreProducts.Count(s => s.IsAvailable),
                InventoryItemCount = x.Inventories.Count,
                InventoryQuantity = x.Inventories.Sum(i => (decimal?)i.Quantity) ?? 0m,
                OrderItemCount = x.OrderItems.Count
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error, Guid? ProductId)> CreateProductAsync(
        ProductFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var categoryError = await ValidateCategoryAsync(tenantId, model.CategoryId, cancellationToken);
        if (categoryError is not null)
        {
            return (false, categoryError, null);
        }

        var sku = NormalizeSku(model.Sku);
        var barcode = TrimToNull(model.Barcode);
        var uniquenessError = await ValidateUniqueIdentifiersAsync(tenantId, null, sku, barcode, cancellationToken);
        if (uniquenessError is not null)
        {
            return (false, uniquenessError, null);
        }

        var maxProducts = await GetMaxProductsAsync(tenantId, cancellationToken);
        if (maxProducts.HasValue)
        {
            var currentCount = await _db.Products.CountAsync(
                x => x.TenantId == tenantId && !x.IsDeleted,
                cancellationToken);
            if (currentCount >= maxProducts.Value)
            {
                return (false, $"Product limit reached for current subscription plan ({maxProducts.Value}).", null);
            }
        }

        var imageResult = await SaveImageAsync(model.ImageFile, cancellationToken);
        if (!imageResult.Succeeded)
        {
            return (false, imageResult.Error, null);
        }

        var now = DateTime.UtcNow;
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = model.CategoryId,
            Name = model.Name.Trim(),
            Sku = sku,
            Barcode = barcode,
            Description = TrimToNull(model.Description),
            Price = model.Price,
            CostPrice = model.CostPrice,
            ImageUrl = imageResult.ImageUrl,
            IsActive = model.IsActive,
            CreatedAt = now,
            CreatedBy = _currentUser.UserId
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "CreateProduct",
            nameof(Product),
            product.Id.ToString(),
            newValue: $"Name={product.Name}; SKU={product.Sku}; Active={product.IsActive}",
            tenantId: tenantId,
            cancellationToken: cancellationToken);

        return (true, null, product.Id);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateProductAsync(
        Guid id,
        ProductFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var product = await _db.Products.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (product is null)
        {
            return (false, "Product not found.");
        }

        var categoryError = await ValidateCategoryAsync(tenantId, model.CategoryId, cancellationToken);
        if (categoryError is not null)
        {
            return (false, categoryError);
        }

        var sku = NormalizeSku(model.Sku);
        var barcode = TrimToNull(model.Barcode);
        var uniquenessError = await ValidateUniqueIdentifiersAsync(tenantId, id, sku, barcode, cancellationToken);
        if (uniquenessError is not null)
        {
            return (false, uniquenessError);
        }

        var imageResult = await SaveImageAsync(model.ImageFile, cancellationToken);
        if (!imageResult.Succeeded)
        {
            return (false, imageResult.Error);
        }

        var oldValue = $"Name={product.Name}; SKU={product.Sku}; Barcode={product.Barcode}; Active={product.IsActive}";
        product.Name = model.Name.Trim();
        product.CategoryId = model.CategoryId;
        product.Sku = sku;
        product.Barcode = barcode;
        product.Description = TrimToNull(model.Description);
        product.Price = model.Price;
        product.CostPrice = model.CostPrice;
        product.IsActive = model.IsActive;
        if (!string.IsNullOrWhiteSpace(imageResult.ImageUrl))
        {
            product.ImageUrl = imageResult.ImageUrl;
        }

        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "UpdateProduct",
            nameof(Product),
            product.Id.ToString(),
            oldValue,
            $"Name={product.Name}; SKU={product.Sku}; Barcode={product.Barcode}; Active={product.IsActive}",
            tenantId,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> ToggleProductAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var product = await _db.Products.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (product is null)
        {
            return (false, "Product not found.");
        }

        var oldValue = product.IsActive.ToString();
        product.IsActive = isActive;
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            isActive ? "ActivateProduct" : "DeactivateProduct",
            nameof(Product),
            product.Id.ToString(),
            oldValue,
            isActive.ToString(),
            tenantId,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> DeleteProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var product = await _db.Products.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (product is null)
        {
            return (false, "Product not found.");
        }

        product.IsDeleted = true;
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "DeleteProduct",
            nameof(Product),
            product.Id.ToString(),
            newValue: $"SoftDeleted=True; Name={product.Name}; SKU={product.Sku}",
            tenantId: tenantId,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    private async Task<string?> ValidateCategoryAsync(
        Guid tenantId,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return null;
        }

        var exists = await _db.Categories.AnyAsync(
            x => x.Id == categoryId.Value && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        return exists ? null : "Selected category is invalid for this tenant.";
    }

    private async Task<string?> ValidateUniqueIdentifiersAsync(
        Guid tenantId,
        Guid? currentProductId,
        string? sku,
        string? barcode,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sku))
        {
            var skuExists = await _db.Products.AnyAsync(
                x => x.TenantId == tenantId
                    && x.Id != currentProductId
                    && x.Sku == sku
                    && !x.IsDeleted,
                cancellationToken);
            if (skuExists)
            {
                return "SKU already exists in this tenant.";
            }
        }

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            var barcodeExists = await _db.Products.AnyAsync(
                x => x.TenantId == tenantId
                    && x.Id != currentProductId
                    && x.Barcode == barcode
                    && !x.IsDeleted,
                cancellationToken);
            if (barcodeExists)
            {
                return "Barcode already exists in this tenant.";
            }
        }

        return null;
    }

    private async Task<(bool Succeeded, string? Error, string? ImageUrl)> SaveImageAsync(
        IFormFile? imageFile,
        CancellationToken cancellationToken)
    {
        if (imageFile is null || imageFile.Length == 0)
        {
            return (true, null, null);
        }

        if (imageFile.Length > MaxImageBytes)
        {
            return (false, "Product image must be 5MB or smaller.", null);
        }

        var extension = Path.GetExtension(imageFile.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
        {
            return (false, "Product image must be JPG, PNG, WEBP or GIF.", null);
        }

        if (!string.IsNullOrWhiteSpace(imageFile.ContentType) &&
            !imageFile.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Uploaded file must be an image.", null);
        }

        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
            : _environment.WebRootPath;
        var uploadRoot = Path.Combine(webRoot, "uploads", "products");
        Directory.CreateDirectory(uploadRoot);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var destinationPath = Path.Combine(uploadRoot, fileName);
        await using var stream = File.Create(destinationPath);
        await imageFile.CopyToAsync(stream, cancellationToken);

        return (true, null, $"/uploads/products/{fileName}");
    }

    private async Task<IReadOnlyList<ProductCategoryOptionViewModel>> GetCategoryOptionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _db.Categories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new ProductCategoryOptionViewModel
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<int?> GetMaxProductsAsync(Guid tenantId, CancellationToken cancellationToken)
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
            .Select(x => x.Plan.MaxProducts)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Guid RequireTenantId()
    {
        if (!_currentUser.TenantId.HasValue)
        {
            throw new InvalidOperationException("Current owner does not have a tenant.");
        }

        return _currentUser.TenantId.Value;
    }

    private static string? NormalizeSku(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
