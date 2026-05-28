namespace ChainPOS.ViewModels.Admin.Tenants;

public sealed class TenantDetailsViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? TaxCode { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? OwnerUserId { get; set; }

    public string OwnerName { get; set; } = string.Empty;

    public string OwnerEmail { get; set; } = string.Empty;

    public string OwnerStatus { get; set; } = string.Empty;

    public int StoreCount { get; set; }

    public int StaffCount { get; set; }

    public int ProductCount { get; set; }

    public int OrderCount { get; set; }

    public decimal RevenueTotal { get; set; }
}
