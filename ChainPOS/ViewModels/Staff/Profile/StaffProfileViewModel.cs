namespace ChainPOS.ViewModels.Staff.Profile;

public sealed class StaffProfileViewModel
{
    public string Id { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public string TenantName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public IReadOnlyList<StaffProfileStoreAccessViewModel> StoreAccesses { get; set; } = Array.Empty<StaffProfileStoreAccessViewModel>();

    public int AssignedStoreCount => StoreAccesses.Count;

    public int ActiveStoreCount => StoreAccesses.Count(x => x.CanWork);

    public int DisabledStoreCount => StoreAccesses.Count(x => !x.CanWork);

    public string Initials
    {
        get
        {
            var initials = string.Concat(FullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(x => x[0]))
                .ToUpperInvariant();

            return string.IsNullOrWhiteSpace(initials) ? "S" : initials;
        }
    }
}

public sealed class StaffProfileStoreAccessViewModel
{
    public Guid UserStoreId { get; set; }

    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public string? StoreAddress { get; set; }

    public string? StorePhone { get; set; }

    public string StoreStatus { get; set; } = string.Empty;

    public bool AssignmentIsActive { get; set; }

    public bool StoreIsActive { get; set; }

    public DateTime AssignedAt { get; set; }

    public bool CanWork => AssignmentIsActive && StoreIsActive;
}
