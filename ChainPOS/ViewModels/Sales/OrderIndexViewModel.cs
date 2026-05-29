namespace ChainPOS.ViewModels.Sales;

public sealed class OrderIndexViewModel
{
    public string AreaName { get; set; } = string.Empty;

    public Guid? StoreId { get; set; }

    public string? Search { get; set; }

    public string? Status { get; set; }

    public string? PaymentStatus { get; set; }

    public DateOnly? Date { get; set; }

    public int TotalOrders { get; set; }

    public int CompletedOrders { get; set; }

    public int CancelledOrders { get; set; }

    public decimal RevenueTotal { get; set; }

    public IReadOnlyList<StoreOptionViewModel> Stores { get; set; } = Array.Empty<StoreOptionViewModel>();

    public IReadOnlyList<OrderListItemViewModel> Orders { get; set; } = Array.Empty<OrderListItemViewModel>();
}
