namespace ChainPOS.ViewModels.Sales;

public sealed class OrderItemDetailsViewModel
{
    public string ProductName { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal LineTotal { get; set; }
}
