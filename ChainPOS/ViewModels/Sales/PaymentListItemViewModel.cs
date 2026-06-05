namespace ChainPOS.ViewModels.Sales;

public sealed class PaymentListItemViewModel
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public string? StaffName { get; set; }

    public string Method { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? TransactionCode { get; set; }

    public string Status { get; set; } = string.Empty;

    public string OrderStatus { get; set; } = string.Empty;

    public string OrderPaymentStatus { get; set; } = string.Empty;

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
