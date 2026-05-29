using ChainPOS.ViewModels.Admin.Settings;

namespace ChainPOS.Services.Admin;

public interface IAdminSettingsService
{
    Task<AdminSettingsViewModel> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(AdminSettingsViewModel model, string? currentUserId, CancellationToken cancellationToken = default);
}
