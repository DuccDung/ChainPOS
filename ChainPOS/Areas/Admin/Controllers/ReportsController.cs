using ChainPOS.Constants;
using ChainPOS.Services.Reports;
using ChainPOS.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class ReportsController : Controller
{
    private const string AreaName = "Admin";
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
}
