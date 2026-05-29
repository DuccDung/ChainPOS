namespace ChainPOS.ViewModels.Owner.Products;

public sealed class ProductDetailsViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? Barcode { get; set; }

    public string? CategoryName { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public decimal CostPrice { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int StoreCount { get; set; }

    public int InventoryItemCount { get; set; }

    public decimal InventoryQuantity { get; set; }

    public int OrderItemCount { get; set; }
}
