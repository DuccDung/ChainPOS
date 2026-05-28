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

    public DateTime? PaidAt { get; set; }

    public string? InvoiceUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TenantSubscription Subscription { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
