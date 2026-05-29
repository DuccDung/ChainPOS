namespace ChainPOS.ViewModels.Owner.Categories;

public sealed class CategoryDetailsViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int ProductCount { get; set; }

    public int ActiveProductCount { get; set; }

    public decimal AveragePrice { get; set; }
}
