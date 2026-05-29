using ChainPOS.ViewModels.Admin.Subscriptions;

namespace ChainPOS.ViewModels.Admin.SystemPayments;

public sealed class SystemPaymentIndexViewModel
{
    public Guid? TenantId { get; set; }

    public string? Status { get; set; }

    public string? Search { get; set; }

    public int TotalPayments { get; set; }

    public int PendingPayments { get; set; }

    public int PaidPayments { get; set; }

    public int FailedPayments { get; set; }

    public decimal PaidAmount { get; set; }

    public IReadOnlyList<SubscriptionTenantOptionViewModel> Tenants { get; set; } = Array.Empty<SubscriptionTenantOptionViewModel>();

    public IReadOnlyList<SystemPaymentListItemViewModel> Payments { get; set; } = Array.Empty<SystemPaymentListItemViewModel>();
}
