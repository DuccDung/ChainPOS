using ChainPOS.Constants;
using ChainPOS.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = AppRoles.Staff)]
public class DashboardController : Controller
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _dashboardService.GetStaffDashboardAsync(cancellationToken));
    }
}
