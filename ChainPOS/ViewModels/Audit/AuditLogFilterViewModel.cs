namespace ChainPOS.ViewModels.Audit;

public sealed class AuditLogFilterViewModel
{
    public Guid? TenantId { get; set; }

    public Guid? StoreId { get; set; }

    public string? UserId { get; set; }

    public string? Action { get; set; }

    public string? Search { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
