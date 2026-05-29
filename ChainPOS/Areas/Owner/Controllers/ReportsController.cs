using ChainPOS.Constants;
using ChainPOS.Services.Reports;
using ChainPOS.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class ReportsController : Controller
{
    private const string AreaName = "Owner";
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task<IActionResult> Index(ReportsFilterViewModel filter, CancellationToken cancellationToken)
    {
        var model = await _reportService.GetReportsAsync(AreaName, filter, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Export(ReportsFilterViewModel filter, CancellationToken cancellationToken)
    {
        var export = await _reportService.ExportReportsAsync(AreaName, filter, cancellationToken);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
