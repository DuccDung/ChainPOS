namespace ChainPOS.ViewModels.Dashboard;

public sealed class DashboardViewModel
{
    public string RoleName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public string WelcomeTitle { get; set; } = string.Empty;

    public string WelcomeDescription { get; set; } = string.Empty;

    public string PrimaryActionText { get; set; } = string.Empty;

    public string PrimaryActionUrl { get; set; } = "#";

    public string SecondaryActionText { get; set; } = string.Empty;

    public string SecondaryActionUrl { get; set; } = "#";

    public IReadOnlyList<DashboardMetricViewModel> Metrics { get; set; } = Array.Empty<DashboardMetricViewModel>();

    public IReadOnlyList<DashboardActivityViewModel> Activities { get; set; } = Array.Empty<DashboardActivityViewModel>();

    public IReadOnlyList<DashboardRecentOrderViewModel> RecentOrders { get; set; } = Array.Empty<DashboardRecentOrderViewModel>();
}
