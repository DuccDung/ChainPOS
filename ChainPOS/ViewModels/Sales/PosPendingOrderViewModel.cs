namespace ChainPOS.ViewModels.Sales;

public sealed class PosPendingOrderViewModel
{
    public Guid Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public int ItemCount { get; set; }

    public decimal TotalAmount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string? StaffName { get; set; }

    public DateTime CreatedAt { get; set; }
}
