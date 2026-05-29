using ChainPOS.ViewModels.Admin.SystemPayments;

namespace ChainPOS.ViewModels.Owner.Subscriptions;

public sealed class OwnerSubscriptionIndexViewModel
{
    public string TenantName { get; set; } = string.Empty;

    public OwnerSubscriptionItemViewModel? CurrentSubscription { get; set; }

    public IReadOnlyList<OwnerSubscriptionItemViewModel> SubscriptionHistory { get; set; } = Array.Empty<OwnerSubscriptionItemViewModel>();

    public IReadOnlyList<SystemPaymentListItemViewModel> PaymentHistory { get; set; } = Array.Empty<SystemPaymentListItemViewModel>();
}
