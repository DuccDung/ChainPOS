namespace ChainPOS.ViewModels.Reports;

public sealed class SystemRevenueReportItemViewModel
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public DateOnly? PaidDate { get; set; }

    public long PaymentCount { get; set; }

    public decimal TotalAmount { get; set; }
}
