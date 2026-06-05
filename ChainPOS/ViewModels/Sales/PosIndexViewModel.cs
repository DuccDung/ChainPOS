namespace ChainPOS.ViewModels.Sales;

public sealed class PosIndexViewModel
{
    public string AreaName { get; set; } = string.Empty;

    public Guid? StoreId { get; set; }

    public string? Search { get; set; }

    public Guid? OpenShiftId { get; set; }

    public string? OpenShiftStoreName { get; set; }

    public DateTime? OpenShiftOpenedAt { get; set; }

    public IReadOnlyList<StoreOptionViewModel> Stores { get; set; } = Array.Empty<StoreOptionViewModel>();

    public IReadOnlyList<PosProductViewModel> Products { get; set; } = Array.Empty<PosProductViewModel>();

    public IReadOnlyList<PosPendingOrderViewModel> PendingOrders { get; set; } = Array.Empty<PosPendingOrderViewModel>();

    public bool CanCheckout => StoreId.HasValue && OpenShiftId.HasValue;
}
