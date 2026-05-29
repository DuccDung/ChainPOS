namespace ChainPOS.ViewModels.Owner.Staff;

public sealed class StaffIndexViewModel
{
    public string? Search { get; set; }

    public string? Status { get; set; }

    public int TotalStaff { get; set; }

    public int ActiveStaff { get; set; }

    public int LockedStaff { get; set; }

    public int AssignedStaff { get; set; }

    public int? MaxStaff { get; set; }

    public IReadOnlyList<StaffListItemViewModel> Staff { get; set; } = Array.Empty<StaffListItemViewModel>();
}
