namespace ChainPOS.ViewModels.Inventory;

public sealed class InventoryTransactionListItemViewModel
{
    public string Type { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string StoreName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal BeforeQuantity { get; set; }

    public decimal AfterQuantity { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
}
