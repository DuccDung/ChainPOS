namespace ChainPOS.ViewModels.Dashboard;

public sealed class DashboardMetricViewModel
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Badge { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string Tone { get; set; } = "orange";

    public string Icon { get; set; } = "chart";
}
