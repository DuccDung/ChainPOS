using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class SystemPayment
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SubscriptionId { get; set; }

    public decimal Amount { get; set; }

    public string Method { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? TransactionCode { get; set; }

    public string? ProviderTransactionId { get; set; }

    public string? BankCode { get; set; }

    public string? BankAccountNo { get; set; }

    public string? BankAccountName { get; set; }

    public string? QrContent { get; set; }

    public string? TransferContent { get; set; }

    public DateTime? PaidAt { get; set; }

    public decimal? PaidAmount { get; set; }

    public string? RawResponse { get; set; }

    public DateTime? ExpiredAt { get; set; }

    public string? InvoiceUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual TenantSubscription Subscription { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<SystemPaymentWebhook> Webhooks { get; set; } = new List<SystemPaymentWebhook>();
}
