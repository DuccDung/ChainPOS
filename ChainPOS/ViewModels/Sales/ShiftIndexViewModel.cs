namespace ChainPOS.ViewModels.Sales;

public sealed class ShiftIndexViewModel
{
    public string AreaName { get; set; } = string.Empty;

    public Guid? StoreId { get; set; }

    public string? Status { get; set; }

    public int TotalShifts { get; set; }

    public int OpenShifts { get; set; }

    public int ClosedShifts { get; set; }

    public decimal CashExpectedToday { get; set; }

    public IReadOnlyList<StoreOptionViewModel> Stores { get; set; } = Array.Empty<StoreOptionViewModel>();

    public IReadOnlyList<ShiftListItemViewModel> Shifts { get; set; } = Array.Empty<ShiftListItemViewModel>();
}
