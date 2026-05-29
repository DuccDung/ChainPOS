namespace ChainPOS.ViewModels.Owner.Stores;

public sealed class StoreIndexViewModel
{
    public string? Search { get; set; }

    public string? Status { get; set; }

    public int TotalStores { get; set; }

    public int ActiveStores { get; set; }

    public int InactiveStores { get; set; }

    public int ClosedStores { get; set; }

    public int? MaxStores { get; set; }

    public IReadOnlyList<StoreListItemViewModel> Stores { get; set; } = Array.Empty<StoreListItemViewModel>();
}
