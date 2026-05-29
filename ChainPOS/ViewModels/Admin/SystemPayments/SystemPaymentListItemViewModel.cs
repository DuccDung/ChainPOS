namespace ChainPOS.ViewModels.Admin.SystemPayments;

public sealed class SystemPaymentListItemViewModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public Guid SubscriptionId { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Method { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime? PaidAt { get; set; }

    public string? InvoiceUrl { get; set; }

    public DateTime CreatedAt { get; set; }
}
