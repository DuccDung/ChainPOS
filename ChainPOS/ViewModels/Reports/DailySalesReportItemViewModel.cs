namespace ChainPOS.ViewModels.Reports;

public sealed class DailySalesReportItemViewModel
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public DateOnly? ReportDate { get; set; }

    public long OrderCount { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }
}
