using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class TenantSubscription
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid PlanId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string Status { get; set; } = null!;

    public bool AutoRenew { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public virtual SubscriptionPlan Plan { get; set; } = null!;

    public virtual ICollection<SystemPayment> SystemPayments { get; set; } = new List<SystemPayment>();

    public virtual Tenant Tenant { get; set; } = null!;
}
