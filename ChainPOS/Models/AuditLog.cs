using System;
using System.Collections.Generic;

namespace ChainPOS.Models;

public partial class AuditLog
{
    public long Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? StoreId { get; set; }

    public string? UserId { get; set; }

    public string Action { get; set; } = null!;

    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Store? Store { get; set; }

    public virtual Tenant? Tenant { get; set; }

    public virtual AspNetUser? User { get; set; }
}
