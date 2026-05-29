using ChainPOS.Models;
using ChainPOS.Services.Common;
using ChainPOS.Services.Security;
using ChainPOS.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;

namespace ChainPOS.Services.Reports;

public sealed class ReportService : IReportService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IStoreAccessService _storeAccess;

    public ReportService(
        StoreFlowDbContext db,
        ICurrentUserService currentUser,
        IStoreAccessService storeAccess)
    {
        _db = db;
        _currentUser = currentUser;
        _storeAccess = storeAccess;
    }

    public async Task<ReportsIndexViewModel> GetReportsAsync(
        string areaName,
        ReportsFilterViewModel? filter,
        CancellationToken cancellationToken = default)
    {
        var isAdmin = string.Equals(areaName, "Admin", StringComparison.OrdinalIgnoreCase);
        var effectiveFilter = NormalizeFilter(isAdmin, filter);
        var accessibleStoreIds = isAdmin
            ? Array.Empty<Guid>()
            : await _storeAccess.GetAccessibleStoreIdsAsync(cancellationToken);

        var dailySales = await GetDailySalesAsync(isAdmin, effectiveFilter, accessibleStoreIds, cancellationToken);
        var staffSales = await GetStaffSalesAsync(isAdmin, effectiveFilter, accessibleStoreIds, cancellationToken);
        var inventoryStatus = await GetInventoryStatusAsync(isAdmin, effectiveFilter, accessibleStoreIds, cancellationToken);
        var systemRevenue = isAdmin
            ? await GetSystemRevenueAsync(effectiveFilter, cancellationToken)
            : Array.Empty<SystemRevenueReportItemViewModel>();

        return new ReportsIndexViewModel
        {
            AreaName = areaName,
            IsAdmin = isAdmin,
            Filter = effectiveFilter,
            Tenants = await GetTenantOptionsAsync(isAdmin, effectiveFilter, cancellationToken),
            Stores = await GetStoreOptionsAsync(isAdmin, effectiveFilter, accessibleStoreIds, cancellationToken),
            SalesOrderCount = dailySales.Sum(x => x.OrderCount),
            SalesRevenue = dailySales.Sum(x => x.TotalAmount),
            DiscountTotal = dailySales.Sum(x => x.DiscountAmount),
            TaxTotal = dailySales.Sum(x => x.TaxAmount),
            StaffOrderCount = staffSales.Sum(x => x.OrderCount),
            StaffSalesTotal = staffSales.Sum(x => x.TotalSales),
            InventoryItemCount = inventoryStatus.Count,
            LowStockItemCount = inventoryStatus.Count(x => x.IsLowStock),
            SystemPaymentCount = systemRevenue.Sum(x => x.PaymentCount),
            SystemRevenueTotal = systemRevenue.Sum(x => x.TotalAmount),
            DailySales = dailySales,
            StaffSales = staffSales,
            InventoryStatus = inventoryStatus,
            SystemRevenue = systemRevenue
        };
    }

    public async Task<(byte[] Content, string FileName, string ContentType)> ExportReportsAsync(
        string areaName,
        ReportsFilterViewModel? filter,
        CancellationToken cancellationToken = default)
    {
        var model = await GetReportsAsync(areaName, filter, cancellationToken);
        var workbook = new StringBuilder();
        workbook.AppendLine("<?xml version=\"1.0\"?>");
        workbook.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        workbook.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");

        AppendWorksheet(
            workbook,
            "Daily Sales",
            new[] { "Date", "Tenant", "Store", "Store Code", "Orders", "Subtotal", "Discount", "Tax", "Total" },
            model.DailySales.Select(x => new[]
            {
                x.ReportDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                x.TenantName,
                x.StoreName,
                x.StoreCode,
                x.OrderCount.ToString(),
                x.SubTotal.ToString("0.##"),
                x.DiscountAmount.ToString("0.##"),
                x.TaxAmount.ToString("0.##"),
                x.TotalAmount.ToString("0.##")
            }));

        AppendWorksheet(
            workbook,
            "Staff Sales",
            new[] { "Date", "Tenant", "Store", "Store Code", "Staff", "Orders", "Sales" },
            model.StaffSales.Select(x => new[]
            {
                x.ReportDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                x.TenantName,
                x.StoreName,
                x.StoreCode,
                x.StaffName,
                x.OrderCount.ToString(),
                x.TotalSales.ToString("0.##")
            }));

        AppendWorksheet(
            workbook,
            "Inventory",
            new[] { "Tenant", "Store", "Store Code", "Product", "SKU", "Barcode", "Quantity", "Min Quantity", "Low Stock", "Updated At" },
            model.InventoryStatus.Select(x => new[]
            {
                x.TenantName,
                x.StoreName,
                x.StoreCode,
                x.ProductName,
                x.Sku ?? string.Empty,
                x.Barcode ?? string.Empty,
                x.Quantity.ToString("0.###"),
                x.MinQuantity.ToString("0.###"),
                x.IsLowStock ? "Yes" : "No",
                x.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }));

        if (model.IsAdmin)
        {
            AppendWorksheet(
                workbook,
                "System Revenue",
                new[] { "Paid Date", "Tenant", "Payments", "Amount" },
                model.SystemRevenue.Select(x => new[]
                {
                    x.PaidDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    x.TenantName,
                    x.PaymentCount.ToString(),
                    x.TotalAmount.ToString("0.##")
                }));
        }

        workbook.AppendLine("</Workbook>");
        var fileName = $"chainpos-reports-{DateTime.UtcNow:yyyyMMddHHmmss}.xls";
        return (Encoding.UTF8.GetBytes(workbook.ToString()), fileName, "application/vnd.ms-excel");
    }

    private static void AppendWorksheet(StringBuilder workbook, string name, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        workbook.Append("<Worksheet ss:Name=\"").Append(Xml(name)).AppendLine("\"><Table>");
        workbook.AppendLine("<Row>");
        foreach (var header in headers)
        {
            AppendCell(workbook, header);
        }
        workbook.AppendLine("</Row>");

        foreach (var row in rows)
        {
            workbook.AppendLine("<Row>");
            foreach (var value in row)
            {
                AppendCell(workbook, value);
            }
            workbook.AppendLine("</Row>");
        }

        workbook.AppendLine("</Table></Worksheet>");
    }

    private static void AppendCell(StringBuilder workbook, string? value)
        => workbook.Append("<Cell><Data ss:Type=\"String\">")
            .Append(Xml(value ?? string.Empty))
            .AppendLine("</Data></Cell>");

    private static string Xml(string value) => WebUtility.HtmlEncode(value);

    private ReportsFilterViewModel NormalizeFilter(bool isAdmin, ReportsFilterViewModel? filter)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fromDate = filter?.FromDate ?? today.AddDays(-29);
        var toDate = filter?.ToDate ?? today;
        if (fromDate > toDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        return new ReportsFilterViewModel
        {
            TenantId = isAdmin ? filter?.TenantId : RequireTenantId(),
            StoreId = filter?.StoreId,
            FromDate = fromDate,
            ToDate = toDate
        };
    }

    private async Task<IReadOnlyList<DailySalesReportItemViewModel>> GetDailySalesAsync(
        bool isAdmin,
        ReportsFilterViewModel filter,
        IReadOnlyCollection<Guid> accessibleStoreIds,
        CancellationToken cancellationToken)
    {
        var query =
            from report in _db.VwDailySalesReports.AsNoTracking()
            join tenant in _db.Tenants.AsNoTracking() on report.TenantId equals tenant.Id
            join store in _db.Stores.AsNoTracking() on report.StoreId equals store.Id
            where !tenant.IsDeleted && !store.IsDeleted
            select new { report, tenant, store };

        if (isAdmin)
        {
            if (filter.TenantId.HasValue)
            {
                query = query.Where(x => x.report.TenantId == filter.TenantId.Value);
            }
        }
        else
        {
            query = query.Where(x => filter.TenantId.HasValue
                && x.report.TenantId == filter.TenantId.Value
                && accessibleStoreIds.Contains(x.report.StoreId));
        }

        if (filter.StoreId.HasValue)
        {
            query = query.Where(x => x.report.StoreId == filter.StoreId.Value);
        }

        query = query.Where(x => x.report.ReportDate.HasValue
            && x.report.ReportDate.Value >= filter.FromDate!.Value
            && x.report.ReportDate.Value <= filter.ToDate!.Value);

        return await query
            .OrderByDescending(x => x.report.ReportDate)
            .ThenBy(x => x.tenant.Name)
            .ThenBy(x => x.store.Name)
            .Select(x => new DailySalesReportItemViewModel
            {
                TenantId = x.report.TenantId,
                TenantName = x.tenant.Name,
                StoreId = x.report.StoreId,
                StoreName = x.store.Name,
                StoreCode = x.store.Code,
                ReportDate = x.report.ReportDate,
                OrderCount = x.report.OrderCount ?? 0,
                SubTotal = x.report.SubTotal ?? 0m,
                DiscountAmount = x.report.DiscountAmount ?? 0m,
                TaxAmount = x.report.TaxAmount ?? 0m,
                TotalAmount = x.report.TotalAmount ?? 0m
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<StaffSalesReportItemViewModel>> GetStaffSalesAsync(
        bool isAdmin,
        ReportsFilterViewModel filter,
        IReadOnlyCollection<Guid> accessibleStoreIds,
        CancellationToken cancellationToken)
    {
        var query =
            from report in _db.VwStaffSalesReports.AsNoTracking()
            join tenant in _db.Tenants.AsNoTracking() on report.TenantId equals tenant.Id
            join store in _db.Stores.AsNoTracking() on report.StoreId equals store.Id
            join user in _db.AspNetUsers.AsNoTracking() on report.StaffUserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            where !tenant.IsDeleted && !store.IsDeleted
            select new { report, tenant, store, user };

        if (isAdmin)
        {
            if (filter.TenantId.HasValue)
            {
                query = query.Where(x => x.report.TenantId == filter.TenantId.Value);
            }
        }
        else
        {
            query = query.Where(x => filter.TenantId.HasValue
                && x.report.TenantId == filter.TenantId.Value
                && accessibleStoreIds.Contains(x.report.StoreId));
        }

        if (filter.StoreId.HasValue)
        {
            query = query.Where(x => x.report.StoreId == filter.StoreId.Value);
        }

        query = query.Where(x => x.report.ReportDate.HasValue
            && x.report.ReportDate.Value >= filter.FromDate!.Value
            && x.report.ReportDate.Value <= filter.ToDate!.Value);

        return await query
            .OrderByDescending(x => x.report.ReportDate)
            .ThenByDescending(x => x.report.TotalSales)
            .Select(x => new StaffSalesReportItemViewModel
            {
                TenantId = x.report.TenantId,
                TenantName = x.tenant.Name,
                StoreId = x.report.StoreId,
                StoreName = x.store.Name,
                StoreCode = x.store.Code,
                StaffUserId = x.report.StaffUserId,
                StaffName = x.user != null && x.user.FullName != null ? x.user.FullName : "Unassigned",
                ReportDate = x.report.ReportDate,
                OrderCount = x.report.OrderCount ?? 0,
                TotalSales = x.report.TotalSales ?? 0m
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<InventoryStatusReportItemViewModel>> GetInventoryStatusAsync(
        bool isAdmin,
        ReportsFilterViewModel filter,
        IReadOnlyCollection<Guid> accessibleStoreIds,
        CancellationToken cancellationToken)
    {
        var query =
            from report in _db.VwInventoryStatusReports.AsNoTracking()
            join tenant in _db.Tenants.AsNoTracking() on report.TenantId equals tenant.Id
            join store in _db.Stores.AsNoTracking() on report.StoreId equals store.Id
            where !tenant.IsDeleted && !store.IsDeleted
            select new { report, tenant, store };

        if (isAdmin)
        {
            if (filter.TenantId.HasValue)
            {
                query = query.Where(x => x.report.TenantId == filter.TenantId.Value);
            }
        }
        else
        {
            query = query.Where(x => filter.TenantId.HasValue
                && x.report.TenantId == filter.TenantId.Value
                && accessibleStoreIds.Contains(x.report.StoreId));
        }

        if (filter.StoreId.HasValue)
        {
            query = query.Where(x => x.report.StoreId == filter.StoreId.Value);
        }

        var fromDateTime = filter.FromDate!.Value.ToDateTime(TimeOnly.MinValue);
        var toDateTimeExclusive = filter.ToDate!.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
        query = query.Where(x => x.report.UpdatedAt >= fromDateTime && x.report.UpdatedAt < toDateTimeExclusive);

        return await query
            .OrderByDescending(x => x.report.IsLowStock == true)
            .ThenBy(x => x.store.Name)
            .ThenBy(x => x.report.ProductName)
            .Select(x => new InventoryStatusReportItemViewModel
            {
                TenantId = x.report.TenantId,
                TenantName = x.tenant.Name,
                StoreId = x.report.StoreId,
                StoreName = x.store.Name,
                StoreCode = x.store.Code,
                ProductId = x.report.ProductId,
                ProductName = x.report.ProductName,
                Sku = x.report.Sku,
                Barcode = x.report.Barcode,
                Quantity = x.report.Quantity,
                MinQuantity = x.report.MinQuantity,
                IsLowStock = x.report.IsLowStock == true,
                UpdatedAt = x.report.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<SystemRevenueReportItemViewModel>> GetSystemRevenueAsync(
        ReportsFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var query =
            from report in _db.VwSystemRevenueReports.AsNoTracking()
            join tenant in _db.Tenants.AsNoTracking() on report.TenantId equals tenant.Id
            where !tenant.IsDeleted
            select new { report, tenant };

        if (filter.TenantId.HasValue)
        {
            query = query.Where(x => x.report.TenantId == filter.TenantId.Value);
        }

        query = query.Where(x => x.report.PaidDate.HasValue
            && x.report.PaidDate.Value >= filter.FromDate!.Value
            && x.report.PaidDate.Value <= filter.ToDate!.Value);

        return await query
            .OrderByDescending(x => x.report.PaidDate)
            .ThenBy(x => x.tenant.Name)
            .Select(x => new SystemRevenueReportItemViewModel
            {
                TenantId = x.report.TenantId,
                TenantName = x.tenant.Name,
                PaidDate = x.report.PaidDate,
                PaymentCount = x.report.PaymentCount ?? 0,
                TotalAmount = x.report.TotalAmount ?? 0m
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ReportTenantOptionViewModel>> GetTenantOptionsAsync(
        bool isAdmin,
        ReportsFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        if (isAdmin)
        {
            return await _db.Tenants
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .Select(x => new ReportTenantOptionViewModel
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToListAsync(cancellationToken);
        }

        return await _db.Tenants
            .AsNoTracking()
            .Where(x => x.Id == filter.TenantId && !x.IsDeleted)
            .Select(x => new ReportTenantOptionViewModel
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ReportStoreOptionViewModel>> GetStoreOptionsAsync(
        bool isAdmin,
        ReportsFilterViewModel filter,
        IReadOnlyCollection<Guid> accessibleStoreIds,
        CancellationToken cancellationToken)
    {
        var query = _db.Stores
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (isAdmin)
        {
            if (filter.TenantId.HasValue)
            {
                query = query.Where(x => x.TenantId == filter.TenantId.Value);
            }
        }
        else
        {
            query = query.Where(x => filter.TenantId.HasValue
                && x.TenantId == filter.TenantId.Value
                && accessibleStoreIds.Contains(x.Id));
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new ReportStoreOptionViewModel
            {
                Id = x.Id,
                TenantId = x.TenantId,
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
