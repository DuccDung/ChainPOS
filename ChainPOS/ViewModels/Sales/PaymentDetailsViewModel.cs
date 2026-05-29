namespace ChainPOS.ViewModels.Sales;

public sealed class PaymentDetailsViewModel
{
    public string Method { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? TransactionCode { get; set; }

    public DateTime? PaidAt { get; set; }
}
