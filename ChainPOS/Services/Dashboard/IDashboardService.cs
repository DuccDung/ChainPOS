using ChainPOS.ViewModels.Dashboard;

namespace ChainPOS.Services.Dashboard;

public interface IDashboardService
{
    Task<DashboardViewModel> GetAdminDashboardAsync(CancellationToken cancellationToken = default);

    Task<DashboardViewModel> GetOwnerDashboardAsync(CancellationToken cancellationToken = default);

    Task<DashboardViewModel> GetStaffDashboardAsync(CancellationToken cancellationToken = default);
}
