using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class Shift
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public string OpenedBy { get; set; } = null!;

    public DateTime OpenedAt { get; set; }

    public string? ClosedBy { get; set; }

    public DateTime? ClosedAt { get; set; }

    public decimal OpeningCash { get; set; }

    public decimal? ClosingCash { get; set; }

    public decimal? ExpectedCash { get; set; }

    public decimal? DifferenceAmount { get; set; }

    public string Status { get; set; } = null!;

    public virtual AspNetUser? ClosedByNavigation { get; set; }

    public virtual AspNetUser OpenedByNavigation { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual Store Store { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
