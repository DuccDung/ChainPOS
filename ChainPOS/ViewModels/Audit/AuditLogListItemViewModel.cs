namespace ChainPOS.ViewModels.Audit;

public sealed class AuditLogListItemViewModel
{
    public long Id { get; set; }

    public Guid? TenantId { get; set; }

    public string TenantName { get; set; } = "System";

    public Guid? StoreId { get; set; }

    public string? StoreName { get; set; }

    public string? StoreCode { get; set; }

    public string? UserId { get; set; }

    public string UserName { get; set; } = "System";

    public string? UserEmail { get; set; }

    public string Action { get; set; } = string.Empty;

    public string ActionGroup { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Severity { get; set; } = "info";

    public string SeverityLabel { get; set; } = "Info";

    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }
}
