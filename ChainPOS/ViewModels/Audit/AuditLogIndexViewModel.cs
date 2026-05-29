namespace ChainPOS.ViewModels.Audit;

public sealed class AuditLogIndexViewModel
{
    public string AreaName { get; set; } = "Admin";

    public bool IsAdmin { get; set; }

    public AuditLogFilterViewModel Filter { get; set; } = new();

    public IReadOnlyList<AuditTenantOptionViewModel> Tenants { get; set; } = Array.Empty<AuditTenantOptionViewModel>();

    public IReadOnlyList<AuditStoreOptionViewModel> Stores { get; set; } = Array.Empty<AuditStoreOptionViewModel>();

    public IReadOnlyList<AuditUserOptionViewModel> Users { get; set; } = Array.Empty<AuditUserOptionViewModel>();

    public IReadOnlyList<string> Actions { get; set; } = Array.Empty<string>();

    public IReadOnlyList<AuditLogListItemViewModel> Logs { get; set; } = Array.Empty<AuditLogListItemViewModel>();

    public int TotalEvents { get; set; }

    public int DistinctUsers { get; set; }

    public int WarningEvents { get; set; }

    public int CriticalEvents { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
