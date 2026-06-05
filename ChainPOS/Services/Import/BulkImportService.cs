using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Admin;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.Services.Inventory;
using ChainPOS.Services.Owner;
using ChainPOS.ViewModels.Admin.Owners;
using ChainPOS.ViewModels.Imports;
using ChainPOS.ViewModels.Inventory;
using ChainPOS.ViewModels.Owner.Categories;
using ChainPOS.ViewModels.Owner.Products;
using ChainPOS.ViewModels.Owner.Staff;
using ChainPOS.ViewModels.Owner.StoreProducts;
using ChainPOS.ViewModels.Owner.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Import;

public sealed class BulkImportService : IBulkImportService
{
    private readonly StoreFlowDbContext _db;
    private readonly IAdminManagementService _adminManagement;
    private readonly IOwnerStaffService _staffService;
    private readonly IOwnerStoreService _storeService;
    private readonly IOwnerCategoryService _categoryService;
    private readonly IOwnerProductService _productService;
    private readonly IOwnerStoreProductService _storeProductService;
    private readonly IInventoryService _inventoryService;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLog;

    public BulkImportService(
        StoreFlowDbContext db,
        IAdminManagementService adminManagement,
        IOwnerStaffService staffService,
        IOwnerStoreService storeService,
        IOwnerCategoryService categoryService,
        IOwnerProductService productService,
        IOwnerStoreProductService storeProductService,
        IInventoryService inventoryService,
        ICurrentUserService currentUser,
        IAuditLogService auditLog)
    {
        _db = db;
        _adminManagement = adminManagement;
        _staffService = staffService;
        _storeService = storeService;
        _categoryService = categoryService;
        _productService = productService;
        _storeProductService = storeProductService;
        _inventoryService = inventoryService;
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public async Task<BulkImportResultViewModel> ImportOwnersAsync(
        IFormFile file,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var result = CreateResult("Owner Import", "Admin", "Owners");
        foreach (var row in await SpreadsheetImportReader.ReadAsync(file, cancellationToken))
        {
            var rowNumber = RowNumber(row);
            var model = new OwnerCreateViewModel
            {
                FullName = Value(row, "FullName", "OwnerName", "Name"),
                Email = Value(row, "Email"),
                PhoneNumber = Value(row, "PhoneNumber", "Phone"),
                TenantName = Value(row, "TenantName", "CompanyName"),
                TaxCode = Value(row, "TaxCode"),
                TenantAddress = Value(row, "TenantAddress", "Address"),
                TenantPhone = Value(row, "TenantPhone"),
                Password = Value(row, "Password"),
                ConfirmPassword = Value(row, "Password")
            };
            if (string.IsNullOrWhiteSpace(model.Password))
            {
                model.Password = model.ConfirmPassword = "Owner@123";
            }

            var create = await _adminManagement.CreateOwnerAsync(model, currentUserId, cancellationToken);
            AddRow(result, rowNumber, create.Succeeded, create.Succeeded ? "Owner and tenant created." : create.Error ?? "Could not create owner.");
        }

        await LogSummaryAsync("BulkImportOwners", result, cancellationToken);
        return result;
    }

    public async Task<BulkImportResultViewModel> ImportStaffAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var result = CreateResult("Staff Import", "Owner", "Staff");
        foreach (var row in await SpreadsheetImportReader.ReadAsync(file, cancellationToken))
        {
            var rowNumber = RowNumber(row);
            var storeCodes = Value(row, "StoreCodes", "Stores")
                .Split(new[] { ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tenantId = _currentUser.TenantId;
            var storesQuery = _db.Stores
                .AsNoTracking()
                .Where(x => storeCodes.Contains(x.Code) && !x.IsDeleted);
            if (tenantId.HasValue)
            {
                storesQuery = storesQuery.Where(x => x.TenantId == tenantId.Value);
            }

            var storeIds = await storesQuery
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var password = Value(row, "Password");
            if (string.IsNullOrWhiteSpace(password))
            {
                password = "Staff@123";
            }

            var model = new StaffCreateViewModel
            {
                FullName = Value(row, "FullName", "Name"),
                Email = Value(row, "Email"),
                PhoneNumber = Value(row, "PhoneNumber", "Phone"),
                Password = password,
                ConfirmPassword = password,
                StoreIds = storeIds
            };

            var create = await _staffService.CreateStaffAsync(model, cancellationToken);
            AddRow(result, rowNumber, create.Succeeded, create.Succeeded ? "Staff created." : create.Error ?? "Could not create staff.");
        }

        await LogSummaryAsync("BulkImportStaff", result, cancellationToken);
        return result;
    }

    public async Task<BulkImportResultViewModel> ImportStoresAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var result = CreateResult("Store Import", "Owner", "Stores");
        foreach (var row in await SpreadsheetImportReader.ReadAsync(file, cancellationToken))
        {
            var rowNumber = RowNumber(row);
            var model = new StoreFormViewModel
            {
                Name = Value(row, "Name", "StoreName"),
                Code = Value(row, "Code", "StoreCode"),
                Address = Value(row, "Address"),
                Phone = Value(row, "Phone", "PhoneNumber"),
                Status = string.IsNullOrWhiteSpace(Value(row, "Status")) ? StoreStatuses.Active : Value(row, "Status")
            };

            var create = await _storeService.CreateStoreAsync(model, cancellationToken);
            AddRow(result, rowNumber, create.Succeeded, create.Succeeded ? "Store created." : create.Error ?? "Could not create store.");
        }

        await LogSummaryAsync("BulkImportStores", result, cancellationToken);
        return result;
    }

    public async Task<BulkImportResultViewModel> ImportCategoriesAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var result = CreateResult("Category Import", "Owner", "Categories");
        foreach (var row in await SpreadsheetImportReader.ReadAsync(file, cancellationToken))
        {
            var rowNumber = RowNumber(row);
            var model = new CategoryFormViewModel
            {
                Name = Value(row, "Name", "CategoryName", "Category"),
                Description = TrimToNull(Value(row, "Description")),
                IsActive = SpreadsheetImportReader.ReadBool(row, "IsActive", true)
            };

            var create = await _categoryService.CreateCategoryAsync(model, cancellationToken);
            AddRow(result, rowNumber, create.Succeeded, create.Succeeded ? "Category created." : create.Error ?? "Could not create category.");
        }

        await LogSummaryAsync("BulkImportCategories", result, cancellationToken);
        return result;
    }

    public async Task<BulkImportResultViewModel> ImportProductsAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var result = CreateResult("Product Import", "Owner", "Products");
        foreach (var row in await SpreadsheetImportReader.ReadAsync(file, cancellationToken))
        {
            var rowNumber = RowNumber(row);
            var categoryName = Value(row, "CategoryName", "Category");
            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                categoryId = await ResolveOrCreateCategoryAsync(categoryName, cancellationToken);
            }

            var model = new ProductFormViewModel
            {
                Name = Value(row, "Name", "ProductName"),
                CategoryId = categoryId,
                Sku = Value(row, "Sku", "SKU"),
                Barcode = Value(row, "Barcode"),
                Description = Value(row, "Description"),
                Price = SpreadsheetImportReader.ReadDecimal(row, "Price"),
                CostPrice = SpreadsheetImportReader.ReadDecimal(row, "CostPrice"),
                IsActive = SpreadsheetImportReader.ReadBool(row, "IsActive", true)
            };

            var create = await _productService.CreateProductAsync(model, cancellationToken);
            if (create.Succeeded && create.ProductId.HasValue)
            {
                var imageUrl = Value(row, "ImageUrl", "Image");
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == create.ProductId.Value, cancellationToken);
                    if (product is not null)
                    {
                        product.ImageUrl = imageUrl;
                        product.UpdatedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            AddRow(result, rowNumber, create.Succeeded, create.Succeeded ? "Product created." : create.Error ?? "Could not create product.");
        }

        await LogSummaryAsync("BulkImportProducts", result, cancellationToken);
        return result;
    }

    public async Task<BulkImportResultViewModel> ImportStoreProductsAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var result = CreateResult("Store Product Import", "Owner", "StoreProducts");
        foreach (var row in await SpreadsheetImportReader.ReadAsync(file, cancellationToken))
        {
            var rowNumber = RowNumber(row);
            var storeCode = Value(row, "StoreCode", "Store");
            var sku = Value(row, "Sku", "SKU", "ProductSku");
            var tenantId = _currentUser.TenantId;
            var storesQuery = _db.Stores
                .AsNoTracking()
                .Where(x => x.Code == storeCode && !x.IsDeleted);
            var productsQuery = _db.Products
                .AsNoTracking()
                .Where(x => x.Sku == sku && !x.IsDeleted);
            if (tenantId.HasValue)
            {
                storesQuery = storesQuery.Where(x => x.TenantId == tenantId.Value);
                productsQuery = productsQuery.Where(x => x.TenantId == tenantId.Value);
            }

            var storeId = await storesQuery
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var productId = await productsQuery
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (!storeId.HasValue || !productId.HasValue)
            {
                AddRow(result, rowNumber, false, "StoreCode or SKU was not found.");
                continue;
            }

            var model = new StoreProductAssignViewModel
            {
                StoreId = storeId,
                ProductId = productId,
                SellingPrice = SpreadsheetImportReader.ReadNullableDecimal(row, "SellingPrice"),
                IsAvailable = SpreadsheetImportReader.ReadBool(row, "IsAvailable", true)
            };

            var assign = await _storeProductService.AssignProductAsync(model, cancellationToken);
            AddRow(result, rowNumber, assign.Succeeded, assign.Succeeded ? "Store product assigned or updated." : assign.Error ?? "Could not assign store product.");
        }

        await LogSummaryAsync("BulkImportStoreProducts", result, cancellationToken);
        return result;
    }

    public async Task<BulkImportResultViewModel> ImportInventoryAsync(
        string areaName,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        var result = CreateResult("Inventory Import", areaName, "Inventory");
        foreach (var row in await SpreadsheetImportReader.ReadAsync(file, cancellationToken))
        {
            var rowNumber = RowNumber(row);
            var storeCode = Value(row, "StoreCode", "Store");
            var sku = Value(row, "Sku", "SKU", "ProductSku");
            var tenantId = _currentUser.TenantId;
            var storesQuery = _db.Stores
                .AsNoTracking()
                .Where(x => x.Code == storeCode && !x.IsDeleted);
            var productsQuery = _db.Products
                .AsNoTracking()
                .Where(x => x.Sku == sku && !x.IsDeleted);
            if (tenantId.HasValue)
            {
                storesQuery = storesQuery.Where(x => x.TenantId == tenantId.Value);
                productsQuery = productsQuery.Where(x => x.TenantId == tenantId.Value);
            }

            var storeId = await storesQuery
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var productId = await productsQuery
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (!storeId.HasValue || !productId.HasValue)
            {
                AddRow(result, rowNumber, false, "StoreCode or SKU was not found.");
                continue;
            }

            var model = new InventoryMovementViewModel
            {
                AreaName = areaName,
                StoreId = storeId,
                ProductId = productId,
                Quantity = SpreadsheetImportReader.ReadDecimal(row, "Quantity"),
                MinQuantity = SpreadsheetImportReader.ReadDecimal(row, "MinQuantity", 0m),
                Reason = Value(row, "Reason")
            };

            var import = await _inventoryService.ImportStockAsync(model, cancellationToken);
            AddRow(result, rowNumber, import.Succeeded, import.Succeeded ? "Stock imported." : import.Error ?? "Could not import stock.");
        }

        await LogSummaryAsync("BulkImportInventory", result, cancellationToken);
        return result;
    }

    private async Task<Guid?> ResolveOrCreateCategoryAsync(string categoryName, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;
        var categoriesQuery = _db.Categories
            .AsNoTracking()
            .Where(x => x.Name == categoryName && !x.IsDeleted);
        if (tenantId.HasValue)
        {
            categoriesQuery = categoriesQuery.Where(x => x.TenantId == tenantId.Value);
        }

        var categoryId = await categoriesQuery
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (categoryId.HasValue)
        {
            return categoryId;
        }

        var create = await _categoryService.CreateCategoryAsync(
            new CategoryFormViewModel
            {
                Name = categoryName,
                IsActive = true
            },
            cancellationToken);

        return create.CategoryId;
    }

    private async Task LogSummaryAsync(
        string action,
        BulkImportResultViewModel result,
        CancellationToken cancellationToken)
    {
        await _auditLog.LogAsync(
            action,
            "BulkImport",
            action,
            newValue: $"Title={result.Title}; Total={result.TotalRows}; Success={result.SuccessRows}; Failed={result.FailedRows}",
            tenantId: _currentUser.TenantId,
            cancellationToken: cancellationToken);
    }

    private static BulkImportResultViewModel CreateResult(string title, string areaName, string backController)
        => new()
        {
            Title = title,
            AreaName = areaName,
            BackController = backController
        };

    private static void AddRow(BulkImportResultViewModel result, int rowNumber, bool succeeded, string message)
        => result.Rows.Add(new BulkImportResultRowViewModel
        {
            RowNumber = rowNumber,
            Succeeded = succeeded,
            Message = message
        });

    private static string Value(Dictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = SpreadsheetImportReader.Get(row, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static int RowNumber(Dictionary<string, string> row)
        => int.TryParse(SpreadsheetImportReader.Get(row, "__row"), out var value) ? value : 0;

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
