namespace ChainPOS.ViewModels.Reports;

public sealed class ReportsIndexViewModel
{
    public string AreaName { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public ReportsFilterViewModel Filter { get; set; } = new();

    public IReadOnlyList<ReportTenantOptionViewModel> Tenants { get; set; } = Array.Empty<ReportTenantOptionViewModel>();

    public IReadOnlyList<ReportStoreOptionViewModel> Stores { get; set; } = Array.Empty<ReportStoreOptionViewModel>();

    public long SalesOrderCount { get; set; }

    public decimal SalesRevenue { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public long StaffOrderCount { get; set; }

    public decimal StaffSalesTotal { get; set; }

    public int InventoryItemCount { get; set; }

    public int LowStockItemCount { get; set; }

    public long SystemPaymentCount { get; set; }

    public decimal SystemRevenueTotal { get; set; }

    public IReadOnlyList<DailySalesReportItemViewModel> DailySales { get; set; } = Array.Empty<DailySalesReportItemViewModel>();

    public IReadOnlyList<StaffSalesReportItemViewModel> StaffSales { get; set; } = Array.Empty<StaffSalesReportItemViewModel>();

    public IReadOnlyList<InventoryStatusReportItemViewModel> InventoryStatus { get; set; } = Array.Empty<InventoryStatusReportItemViewModel>();

    public IReadOnlyList<SystemRevenueReportItemViewModel> SystemRevenue { get; set; } = Array.Empty<SystemRevenueReportItemViewModel>();
}
