using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Owner.StoreProducts;

public sealed class StoreProductAssignViewModel
{
    [Required]
    [Display(Name = "Store")]
    public Guid? StoreId { get; set; }

    [Required]
    [Display(Name = "Product")]
    public Guid? ProductId { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Selling Price")]
    public decimal? SellingPrice { get; set; }

    [Display(Name = "Available")]
    public bool IsAvailable { get; set; } = true;

    public IReadOnlyList<StoreProductStoreOptionViewModel> Stores { get; set; } = Array.Empty<StoreProductStoreOptionViewModel>();

    public IReadOnlyList<StoreProductProductOptionViewModel> Products { get; set; } = Array.Empty<StoreProductProductOptionViewModel>();
}
