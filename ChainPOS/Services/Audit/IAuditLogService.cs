namespace ChainPOS.Services.Audit;

public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string? entityName = null,
        string? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        Guid? tenantId = null,
        Guid? storeId = null,
        CancellationToken cancellationToken = default);
}
