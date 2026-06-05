namespace ChainPOS.ViewModels.Sales;

public sealed class PaymentIndexViewModel
{
    public string AreaName { get; set; } = string.Empty;

    public Guid? StoreId { get; set; }

    public string? Search { get; set; }

    public string? Method { get; set; }

    public string? Status { get; set; }

    public DateOnly? Date { get; set; }

    public int TotalPayments { get; set; }

    public int PaidPayments { get; set; }

    public int PendingPayments { get; set; }

    public int FailedPayments { get; set; }

    public decimal PaidAmount { get; set; }

    public IReadOnlyList<StoreOptionViewModel> Stores { get; set; } = Array.Empty<StoreOptionViewModel>();

    public IReadOnlyList<PaymentListItemViewModel> Payments { get; set; } = Array.Empty<PaymentListItemViewModel>();
}
