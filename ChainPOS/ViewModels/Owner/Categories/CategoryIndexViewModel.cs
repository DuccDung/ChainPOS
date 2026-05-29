namespace ChainPOS.ViewModels.Owner.Categories;

public sealed class CategoryIndexViewModel
{
    public string? Search { get; set; }

    public string? Status { get; set; }

    public int TotalCategories { get; set; }

    public int ActiveCategories { get; set; }

    public int InactiveCategories { get; set; }

    public int ProductCount { get; set; }

    public IReadOnlyList<CategoryListItemViewModel> Categories { get; set; } = Array.Empty<CategoryListItemViewModel>();
}
