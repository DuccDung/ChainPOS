namespace ChainPOS.ViewModels.Admin.Owners;

public sealed class OwnerIndexViewModel
{
    public string? Search { get; set; }

    public string? Status { get; set; }

    public int TotalOwners { get; set; }

    public IReadOnlyList<OwnerListItemViewModel> Owners { get; set; } = Array.Empty<OwnerListItemViewModel>();
}
