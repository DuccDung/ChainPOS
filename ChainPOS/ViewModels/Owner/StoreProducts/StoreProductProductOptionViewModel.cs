namespace ChainPOS.ViewModels.Owner.StoreProducts;

public sealed class StoreProductProductOptionViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public decimal Price { get; set; }
}
