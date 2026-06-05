namespace ChainPOS.ViewModels.Owner.StoreProducts;

public sealed class StoreProductListItemViewModel
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string? Sku { get; set; }

    public string? CategoryName { get; set; }

    public decimal BasePrice { get; set; }

    public decimal? SellingPrice { get; set; }

    public decimal EffectivePrice => SellingPrice ?? BasePrice;

    public bool IsAvailable { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
