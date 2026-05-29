namespace ChainPOS.ViewModels.Admin.Subscriptions;

public sealed class SubscriptionPlanOptionViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string BillingCycle { get; set; } = string.Empty;
}

public sealed class SubscriptionTenantOptionViewModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
