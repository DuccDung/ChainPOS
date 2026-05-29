namespace ChainPOS.ViewModels.Owner.Subscriptions;

public sealed class OwnerSubscriptionItemViewModel
{
    public Guid Id { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string BillingCycle { get; set; } = string.Empty;

    public int? MaxStores { get; set; }

    public int? MaxStaff { get; set; }

    public int? MaxProducts { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool AutoRenew { get; set; }

    public bool IsExpired { get; set; }
}
