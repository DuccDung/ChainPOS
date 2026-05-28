using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class Inventory
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal MinQuantity { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Store Store { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;
}
