namespace ChainPOS.ViewModels.Owner.Products;

public sealed class ProductIndexViewModel
{
    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Status { get; set; }

    public int TotalProducts { get; set; }

    public int ActiveProducts { get; set; }

    public int InactiveProducts { get; set; }

    public int CategoryCount { get; set; }

    public int? MaxProducts { get; set; }

    public IReadOnlyList<ProductCategoryOptionViewModel> Categories { get; set; } = Array.Empty<ProductCategoryOptionViewModel>();

    public IReadOnlyList<ProductListItemViewModel> Products { get; set; } = Array.Empty<ProductListItemViewModel>();
}
