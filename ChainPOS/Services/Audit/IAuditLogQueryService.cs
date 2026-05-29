using ChainPOS.ViewModels.Audit;

namespace ChainPOS.Services.Audit;

public interface IAuditLogQueryService
{
    Task<AuditLogIndexViewModel> GetAuditLogsAsync(
        string areaName,
        AuditLogFilterViewModel? filter,
        CancellationToken cancellationToken = default);
}
