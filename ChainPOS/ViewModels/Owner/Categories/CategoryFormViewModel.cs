using System.ComponentModel.DataAnnotations;

namespace ChainPOS.ViewModels.Owner.Categories;

public sealed class CategoryFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
