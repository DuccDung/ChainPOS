namespace ChainPOS.ViewModels.Owner.Staff;

public sealed class StaffStoreAssignmentViewModel
{
    public Guid UserStoreId { get; set; }

    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = string.Empty;

    public string StoreCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}
