using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Inventory;

public sealed class InventoryAdjustViewModel
{
    public string AreaName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Store")]
    public Guid? StoreId { get; set; }

    [Required]
    [Display(Name = "Product")]
    public Guid? ProductId { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Actual Quantity")]
    public decimal ActualQuantity { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Min Quantity")]
    public decimal MinQuantity { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public IReadOnlyList<InventoryStoreOptionViewModel> Stores { get; set; } = Array.Empty<InventoryStoreOptionViewModel>();

    public IReadOnlyList<InventoryProductOptionViewModel> Products { get; set; } = Array.Empty<InventoryProductOptionViewModel>();
}
