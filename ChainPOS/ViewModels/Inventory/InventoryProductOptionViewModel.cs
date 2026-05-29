namespace ChainPOS.ViewModels.Inventory;

public sealed class InventoryProductOptionViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }
}
