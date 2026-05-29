namespace ChainPOS.ViewModels.Dashboard;

public sealed class DashboardRecentOrderViewModel
{
    public Guid Id { get; set; }

    public string OrderCode { get; set; } = string.Empty;

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public string? StaffName { get; set; }

    public decimal TotalAmount { get; set; }

    public string OrderStatus { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
