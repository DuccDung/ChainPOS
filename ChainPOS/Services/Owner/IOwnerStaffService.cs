using ChainPOS.ViewModels.Owner.Staff;

namespace ChainPOS.Services.Owner;

public interface IOwnerStaffService
{
    Task<StaffIndexViewModel> GetStaffAsync(string? search, string? status, CancellationToken cancellationToken = default);

    Task<StaffCreateViewModel> GetCreateFormAsync(StaffCreateViewModel? model = null, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, string? StaffId)> CreateStaffAsync(StaffCreateViewModel model, CancellationToken cancellationToken = default);

    Task<StaffDetailsViewModel?> GetStaffDetailsAsync(string id, CancellationToken cancellationToken = default);

    Task<StaffResetPasswordViewModel?> GetResetPasswordFormAsync(string id, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> ResetPasswordAsync(string id, StaffResetPasswordViewModel model, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SetStaffStatusAsync(string id, string status, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> AssignStoreAsync(string staffId, Guid storeId, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> SetStoreAssignmentStatusAsync(string staffId, Guid userStoreId, bool isActive, CancellationToken cancellationToken = default);
}
