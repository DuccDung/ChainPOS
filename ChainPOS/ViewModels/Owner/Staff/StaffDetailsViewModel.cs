using ChainPOS.Constants;

namespace ChainPOS.ViewModels.Owner.Staff;

public sealed class StaffDetailsViewModel
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public IReadOnlyList<StaffStoreAssignmentViewModel> StoreAssignments { get; set; } = Array.Empty<StaffStoreAssignmentViewModel>();

    public IReadOnlyList<StaffStoreOptionViewModel> AvailableStores { get; set; } = Array.Empty<StaffStoreOptionViewModel>();

    public int ActiveStoreCount => StoreAssignments.Count(x => x.IsActive);

    public bool IsLocked => string.Equals(Status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase);
}
