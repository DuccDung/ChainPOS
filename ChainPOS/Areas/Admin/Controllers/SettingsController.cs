using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Services.Admin;
using ChainPOS.ViewModels.Admin.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class SettingsController : Controller
{
    private readonly IAdminSettingsService _settingsService;

    public SettingsController(IAdminSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _settingsService.GetSettingsAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AdminSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _settingsService.SaveSettingsAsync(model, CurrentUserId(), cancellationToken);
        TempData["SuccessMessage"] = "System settings saved.";
        return RedirectToAction(nameof(Index));
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
