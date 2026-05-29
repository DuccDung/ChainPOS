using ChainPOS.Models;
using ChainPOS.Services.Common;

namespace ChainPOS.Services.Audit;

public sealed class AuditLogService : IAuditLogService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditLogService(
        StoreFlowDbContext db,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        string action,
        string? entityName = null,
        string? entityId = null,
        string? oldValue = null,
        string? newValue = null,
        Guid? tenantId = null,
        Guid? storeId = null,
        CancellationToken cancellationToken = default)
    {
        await WriteLogAsync(
            action,
            _currentUser.UserId,
            entityName,
            entityId,
            oldValue,
            newValue,
            tenantId ?? _currentUser.TenantId,
            storeId,
            cancellationToken);
    }

    public async Task LogForUserAsync(
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
        await WriteLogAsync(
            action,
            userId,
            entityName,
            entityId,
            oldValue,
            newValue,
            tenantId,
            storeId,
            cancellationToken);
    }

    private async Task WriteLogAsync(
        string action,
        string? userId,
        string? entityName,
        string? entityId,
        string? oldValue,
        string? newValue,
        Guid? tenantId,
        Guid? storeId,
        CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            StoreId = storeId,
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
