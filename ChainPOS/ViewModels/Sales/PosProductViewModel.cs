namespace ChainPOS.ViewModels.Sales;

public sealed class PosProductViewModel
{
    public Guid ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public string? CategoryName { get; set; }

    public string? ImageUrl { get; set; }

    public decimal Price { get; set; }

    public decimal QuantityOnHand { get; set; }

    public bool IsLowStock { get; set; }
}
