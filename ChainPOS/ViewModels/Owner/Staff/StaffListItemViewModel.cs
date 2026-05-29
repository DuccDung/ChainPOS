using ChainPOS.Constants;

namespace ChainPOS.ViewModels.Owner.Staff;

public sealed class StaffListItemViewModel
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public int AssignedStoreCount { get; set; }

    public int ActiveStoreCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsLocked => string.Equals(Status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase);
}
