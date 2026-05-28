using ChainPOS.Constants;

namespace ChainPOS.ViewModels.Admin.Owners;

public sealed class OwnerDetailsViewModel
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public Guid? TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public string TenantStatus { get; set; } = string.Empty;

    public string? TenantPhone { get; set; }

    public string? TenantAddress { get; set; }

    public int StoreCount { get; set; }

    public int StaffCount { get; set; }

    public int ProductCount { get; set; }

    public int OrderCount { get; set; }

    public decimal RevenueTotal { get; set; }

    public bool IsLocked => string.Equals(Status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase);
}
