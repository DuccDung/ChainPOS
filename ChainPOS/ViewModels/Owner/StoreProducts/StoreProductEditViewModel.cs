using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Owner.StoreProducts;

public sealed class StoreProductEditViewModel
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public decimal BasePrice { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Selling Price")]
    public decimal? SellingPrice { get; set; }

    [Display(Name = "Available")]
    public bool IsAvailable { get; set; } = true;
}
