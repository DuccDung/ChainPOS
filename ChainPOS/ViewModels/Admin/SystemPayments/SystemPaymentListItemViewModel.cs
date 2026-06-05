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

    public string? TransactionCode { get; set; }

    public string? TransferContent { get; set; }

    public string? QrImageUrl { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public decimal? PaidAmount { get; set; }

    public string? InvoiceUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsSePay { get; set; }

    public bool IsExpired { get; set; }
}
