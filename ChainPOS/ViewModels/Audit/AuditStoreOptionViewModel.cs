namespace ChainPOS.ViewModels.Audit;

public sealed class AuditStoreOptionViewModel
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}
