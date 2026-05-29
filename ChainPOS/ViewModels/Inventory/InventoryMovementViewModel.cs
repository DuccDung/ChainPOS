using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Inventory;

public sealed class InventoryMovementViewModel
{
    public string AreaName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Store")]
    public Guid? StoreId { get; set; }

    [Required]
    [Display(Name = "Product")]
    public Guid? ProductId { get; set; }

    [Range(typeof(decimal), "0.001", "9999999999999999")]
    public decimal Quantity { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Min Quantity")]
    public decimal MinQuantity { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    public IReadOnlyList<InventoryStoreOptionViewModel> Stores { get; set; } = Array.Empty<InventoryStoreOptionViewModel>();

    public IReadOnlyList<InventoryProductOptionViewModel> Products { get; set; } = Array.Empty<InventoryProductOptionViewModel>();
}
