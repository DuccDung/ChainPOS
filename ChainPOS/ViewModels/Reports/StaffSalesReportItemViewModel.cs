namespace ChainPOS.ViewModels.Reports;

public sealed class StaffSalesReportItemViewModel
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public string? StaffUserId { get; set; }

    public string StaffName { get; set; } = "Unassigned";

    public DateOnly? ReportDate { get; set; }

    public long OrderCount { get; set; }

    public decimal TotalSales { get; set; }
}
