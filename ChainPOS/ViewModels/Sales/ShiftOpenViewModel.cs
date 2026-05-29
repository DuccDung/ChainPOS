using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Sales;

public sealed class ShiftOpenViewModel
{
    public string AreaName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Store")]
    public Guid? StoreId { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Opening Cash")]
    public decimal OpeningCash { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }

    public IReadOnlyList<StoreOptionViewModel> Stores { get; set; } = Array.Empty<StoreOptionViewModel>();
}
