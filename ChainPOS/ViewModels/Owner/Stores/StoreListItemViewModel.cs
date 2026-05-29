using ChainPOS.Constants;

namespace ChainPOS.ViewModels.Owner.Stores;

public sealed class StoreListItemViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string Status { get; set; } = string.Empty;

    public int StaffCount { get; set; }

    public int ProductCount { get; set; }

    public int OrderCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsActive => string.Equals(Status, StoreStatuses.Active, StringComparison.OrdinalIgnoreCase);
}
