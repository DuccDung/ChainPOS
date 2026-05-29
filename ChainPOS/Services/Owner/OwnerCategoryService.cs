using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.ViewModels.Owner.Categories;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Owner;

public sealed class OwnerCategoryService : IOwnerCategoryService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public OwnerCategoryService(
        StoreFlowDbContext db,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _db = db;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<CategoryIndexViewModel> GetCategoriesAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var query = _db.Categories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(x =>
                x.Name.Contains(trimmedSearch) ||
                (x.Description != null && x.Description.Contains(trimmedSearch)));
        }

        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
        }
        else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
        }

        var categories = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CategoryListItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsActive = x.IsActive,
                ProductCount = x.Products.Count(p => !p.IsDeleted),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var baseQuery = _db.Categories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);

        return new CategoryIndexViewModel
        {
            Search = trimmedSearch,
            Status = status,
            TotalCategories = await baseQuery.CountAsync(cancellationToken),
            ActiveCategories = await baseQuery.CountAsync(x => x.IsActive, cancellationToken),
            InactiveCategories = await baseQuery.CountAsync(x => !x.IsActive, cancellationToken),
            ProductCount = await _db.Products.CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken),
            Categories = categories
        };
    }

    public async Task<CategoryDetailsViewModel?> GetCategoryDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var category = await _db.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        if (category is null)
        {
            return null;
        }

        var productQuery = _db.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CategoryId == id && !x.IsDeleted);

        return new CategoryDetailsViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt,
            ProductCount = await productQuery.CountAsync(cancellationToken),
            ActiveProductCount = await productQuery.CountAsync(x => x.IsActive, cancellationToken),
            AveragePrice = await productQuery.AverageAsync(x => (decimal?)x.Price, cancellationToken) ?? 0m
        };
    }

    public async Task<CategoryFormViewModel?> GetCategoryFormAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        return await _db.Categories
            .AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new CategoryFormViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error, Guid? CategoryId)> CreateCategoryAsync(
        CategoryFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var name = model.Name.Trim();
        var exists = await _db.Categories.AnyAsync(
            x => x.TenantId == tenantId && x.Name == name && !x.IsDeleted,
            cancellationToken);
        if (exists)
        {
            return (false, "Category name already exists in this tenant.", null);
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = TrimToNull(model.Description),
            IsActive = model.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "CreateCategory",
            nameof(Category),
            category.Id.ToString(),
            newValue: $"Name={category.Name}; Active={category.IsActive}",
            tenantId: tenantId,
            cancellationToken: cancellationToken);

        return (true, null, category.Id);
    }

    public async Task<(bool Succeeded, string? Error)> UpdateCategoryAsync(
        Guid id,
        CategoryFormViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var category = await _db.Categories.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (category is null)
        {
            return (false, "Category not found.");
        }

        var name = model.Name.Trim();
        var exists = await _db.Categories.AnyAsync(
            x => x.TenantId == tenantId && x.Id != id && x.Name == name && !x.IsDeleted,
            cancellationToken);
        if (exists)
        {
            return (false, "Category name already exists in this tenant.");
        }

        var oldValue = $"Name={category.Name}; Active={category.IsActive}";
        category.Name = name;
        category.Description = TrimToNull(model.Description);
        category.IsActive = model.IsActive;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            "UpdateCategory",
            nameof(Category),
            category.Id.ToString(),
            oldValue,
            $"Name={category.Name}; Active={category.IsActive}",
            tenantId,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> ToggleCategoryAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var category = await _db.Categories.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (category is null)
        {
            return (false, "Category not found.");
        }

        var oldValue = category.IsActive.ToString();
        category.IsActive = isActive;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            isActive ? "ActivateCategory" : "DeactivateCategory",
            nameof(Category),
            category.Id.ToString(),
            oldValue,
            isActive.ToString(),
            tenantId,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> DeleteCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var category = await _db.Categories.FirstOrDefaultAsync(
            x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (category is null)
        {
            return (false, "Category not found.");
        }

        category.IsDeleted = true;
        category.IsActive = false;
        category.UpdatedAt = DateTime.UtcNow;
        category.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "DeleteCategory",
            nameof(Category),
            category.Id.ToString(),
            newValue: $"SoftDeleted=True; Name={category.Name}",
            tenantId: tenantId,
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

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
