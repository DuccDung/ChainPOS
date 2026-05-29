namespace ChainPOS.ViewModels.Sales;

public sealed class OrderListItemViewModel
{
    public Guid Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public string? StaffName { get; set; }

    public int ItemCount { get; set; }

    public decimal TotalAmount { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;

    public string OrderStatus { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
