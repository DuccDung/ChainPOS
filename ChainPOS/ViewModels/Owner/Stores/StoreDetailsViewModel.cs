namespace ChainPOS.ViewModels.Owner.Stores;

public sealed class StoreDetailsViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int StaffCount { get; set; }

    public int ProductCount { get; set; }

    public int InventoryItemCount { get; set; }

    public int LowStockCount { get; set; }

    public int OrderCount { get; set; }

    public decimal RevenueTotal { get; set; }
}
