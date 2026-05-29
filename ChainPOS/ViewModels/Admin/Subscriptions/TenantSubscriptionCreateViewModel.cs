using System.ComponentModel.DataAnnotations;
using ChainPOS.Constants;

namespace ChainPOS.ViewModels.Admin.Subscriptions;

public sealed class TenantSubscriptionCreateViewModel
{
    [Required]
    public Guid? TenantId { get; set; }

    [Required]
    public Guid? PlanId { get; set; }

    [Required]
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? EndDate { get; set; }

    [Required]
    public string Status { get; set; } = SubscriptionStatuses.Active;

    public bool AutoRenew { get; set; } = true;

    public bool CreatePendingPayment { get; set; } = true;

    public string PaymentMethod { get; set; } = PaymentMethods.BankTransfer;

    [StringLength(500)]
    public string? InvoiceUrl { get; set; }

    public IReadOnlyList<SubscriptionTenantOptionViewModel> Tenants { get; set; } = Array.Empty<SubscriptionTenantOptionViewModel>();

    public IReadOnlyList<SubscriptionPlanOptionViewModel> Plans { get; set; } = Array.Empty<SubscriptionPlanOptionViewModel>();
}
