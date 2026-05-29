namespace ChainPOS.ViewModels.Inventory;

public sealed class InventoryListItemViewModel
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public string? CategoryName { get; set; }

    public decimal Quantity { get; set; }

    public decimal MinQuantity { get; set; }

    public bool IsLowStock => Quantity <= MinQuantity;

    public DateTime UpdatedAt { get; set; }
}
