namespace ChainPOS.ViewModels.Admin.Tenants;

public sealed class TenantListItemViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string OwnerName { get; set; } = string.Empty;

    public string OwnerEmail { get; set; } = string.Empty;

    public int StoreCount { get; set; }

    public int StaffCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
