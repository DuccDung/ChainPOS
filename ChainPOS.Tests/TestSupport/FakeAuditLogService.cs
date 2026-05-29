using ChainPOS.Services.Audit;

namespace ChainPOS.Tests.TestSupport;

internal sealed class FakeAuditLogService : IAuditLogService
{
    public List<string> Actions { get; } = new();

    public Task LogAsync(
        string action,
        string? entityName = null,
        string? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        Guid? tenantId = null,
        Guid? storeId = null,
        CancellationToken cancellationToken = default)
    {
        Actions.Add(action);
        return Task.CompletedTask;
    }

    public Task LogForUserAsync(
        string action,
        string? userId,
        string? entityName = null,
        string? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        Guid? tenantId = null,
        Guid? storeId = null,
        CancellationToken cancellationToken = default)
    {
        Actions.Add(action);
        return Task.CompletedTask;
    }
}
