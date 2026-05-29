namespace ChainPOS.ViewModels.Admin.SubscriptionPlans;

public sealed class SubscriptionPlanIndexViewModel
{
    public string? Search { get; set; }

    public string? Status { get; set; }

    public int TotalPlans { get; set; }

    public int ActivePlans { get; set; }

    public int InactivePlans { get; set; }

    public int TenantSubscriptions { get; set; }

    public IReadOnlyList<SubscriptionPlanListItemViewModel> Plans { get; set; } = Array.Empty<SubscriptionPlanListItemViewModel>();
}
