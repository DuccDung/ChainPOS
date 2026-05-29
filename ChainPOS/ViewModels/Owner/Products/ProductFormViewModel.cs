using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ChainPOS.ViewModels.Owner.Products;

public sealed class ProductFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(250)]
    [Display(Name = "Product Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Category")]
    public Guid? CategoryId { get; set; }

    [StringLength(64)]
    [Display(Name = "SKU Code")]
    public string? Sku { get; set; }

    [StringLength(128)]
    public string? Barcode { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal Price { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999")]
    [Display(Name = "Cost Price")]
    public decimal CostPrice { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Product Image")]
    public IFormFile? ImageFile { get; set; }

    public string? ExistingImageUrl { get; set; }

    public IReadOnlyList<ProductCategoryOptionViewModel> Categories { get; set; } = Array.Empty<ProductCategoryOptionViewModel>();
}
