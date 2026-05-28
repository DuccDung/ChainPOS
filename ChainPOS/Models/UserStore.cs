using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class UserStore
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string UserId { get; set; } = null!;

    public Guid StoreId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public virtual Store Store { get; set; } = null!;

    public virtual Tenant Tenant { get; set; } = null!;

    public virtual AspNetUser User { get; set; } = null!;
}
