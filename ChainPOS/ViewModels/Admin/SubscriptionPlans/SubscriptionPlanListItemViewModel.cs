namespace ChainPOS.ViewModels.Admin.SubscriptionPlans;

public sealed class SubscriptionPlanListItemViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string BillingCycle { get; set; } = string.Empty;

    public int? MaxStores { get; set; }

    public int? MaxStaff { get; set; }

    public int? MaxProducts { get; set; }

    public bool IsActive { get; set; }

    public int ActiveTenantCount { get; set; }

    public int TotalTenantCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
