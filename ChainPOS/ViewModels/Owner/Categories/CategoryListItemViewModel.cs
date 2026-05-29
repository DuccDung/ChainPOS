namespace ChainPOS.ViewModels.Owner.Categories;

public sealed class CategoryListItemViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public int ProductCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
