namespace ChainPOS.ViewModels.Inventory;

public sealed class InventoryIndexViewModel
{
    public string AreaName { get; set; } = string.Empty;

    public Guid? StoreId { get; set; }

    public string? Search { get; set; }

    public string? StockStatus { get; set; }

    public int TotalItems { get; set; }

    public int LowStockItems { get; set; }

    public int OutOfStockItems { get; set; }

    public decimal TotalQuantity { get; set; }

    public IReadOnlyList<InventoryStoreOptionViewModel> Stores { get; set; } = Array.Empty<InventoryStoreOptionViewModel>();

    public IReadOnlyList<InventoryListItemViewModel> Items { get; set; } = Array.Empty<InventoryListItemViewModel>();
}
