namespace ChainPOS.ViewModels.Owner.StoreProducts;

public sealed class StoreProductIndexViewModel
{
    public Guid? StoreId { get; set; }

    public string? Search { get; set; }

    public string? Availability { get; set; }

    public int TotalAssignments { get; set; }

    public int AvailableAssignments { get; set; }

    public int UnavailableAssignments { get; set; }

    public int StoreCount { get; set; }

    public int ProductCount { get; set; }

    public IReadOnlyList<StoreProductStoreOptionViewModel> Stores { get; set; } = Array.Empty<StoreProductStoreOptionViewModel>();

    public IReadOnlyList<StoreProductListItemViewModel> StoreProducts { get; set; } = Array.Empty<StoreProductListItemViewModel>();
}
