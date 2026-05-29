using System.ComponentModel.DataAnnotations;
using ChainPOS.Constants;

namespace ChainPOS.ViewModels.Owner.Stores;

public sealed class StoreFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Store name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [RegularExpression(@"^[A-Za-z0-9\-_]+$", ErrorMessage = "Store code can only contain letters, numbers, hyphen and underscore.")]
    [Display(Name = "Store code")]
    public string Code { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [Required]
    public string Status { get; set; } = StoreStatuses.Active;
}
