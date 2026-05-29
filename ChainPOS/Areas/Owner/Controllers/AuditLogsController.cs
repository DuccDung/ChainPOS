using ChainPOS.Constants;
using ChainPOS.Services.Audit;
using ChainPOS.ViewModels.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class AuditLogsController : Controller
{
    private const string AreaName = "Owner";
    private readonly IAuditLogQueryService _auditLogQueryService;

    public AuditLogsController(IAuditLogQueryService auditLogQueryService)
    {
        _auditLogQueryService = auditLogQueryService;
    }

    public async Task<IActionResult> Index(AuditLogFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await _auditLogQueryService.GetAuditLogsAsync(AreaName, filter, cancellationToken);
        return View(model);
    }
}
