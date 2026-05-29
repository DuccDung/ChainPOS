namespace ChainPOS.ViewModels.Sales;

public sealed class OrderDetailsViewModel
{
    public string AreaName { get; set; } = string.Empty;

    public Guid Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public string? StaffName { get; set; }

    public string? ShiftCode { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;

    public string OrderStatus { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public IReadOnlyList<OrderItemDetailsViewModel> Items { get; set; } = Array.Empty<OrderItemDetailsViewModel>();

    public IReadOnlyList<PaymentDetailsViewModel> Payments { get; set; } = Array.Empty<PaymentDetailsViewModel>();

    public bool CanCancel => !string.Equals(OrderStatus, Constants.OrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase);
}
