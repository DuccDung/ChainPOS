using ChainPOS.Constants;

namespace ChainPOS.ViewModels.Admin.Owners;

public sealed class OwnerListItemViewModel
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public string TenantName { get; set; } = string.Empty;

    public string TenantStatus { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsLocked => string.Equals(Status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase);
}
