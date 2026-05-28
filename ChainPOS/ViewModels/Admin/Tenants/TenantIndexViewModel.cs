namespace ChainPOS.ViewModels.Admin.Tenants;

public sealed class TenantIndexViewModel
{
    public string? Search { get; set; }

    public string? Status { get; set; }

    public int TotalTenants { get; set; }

    public IReadOnlyList<TenantListItemViewModel> Tenants { get; set; } = Array.Empty<TenantListItemViewModel>();
}
