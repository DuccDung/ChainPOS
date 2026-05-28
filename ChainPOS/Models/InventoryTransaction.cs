using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class InventoryTransaction
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    public string Type { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal BeforeQuantity { get; set; }

    public decimal AfterQuantity { get; set; }

    public string? Reason { get; set; }

    public string? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual AspNetUser? CreatedByNavigation { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
