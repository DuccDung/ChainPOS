using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Services.Admin;
using ChainPOS.ViewModels.Admin.Owners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class OwnersController : Controller
{
    private readonly IAdminManagementService _adminManagement;

    public OwnersController(IAdminManagementService adminManagement)
    {
        _adminManagement = adminManagement;
    }

    public async Task<IActionResult> Index(string? search, string? status, CancellationToken cancellationToken)
    {
        var model = await _adminManagement.GetOwnersAsync(search, status, cancellationToken);
        return View(model);
    }

    public IActionResult Create()
    {
        return View(new OwnerCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OwnerCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _adminManagement.CreateOwnerAsync(
            model,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create owner.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Owner and tenant created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var model = await _adminManagement.GetOwnerDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(string id, CancellationToken cancellationToken)
    {
        var result = await _adminManagement.SetOwnerStatusAsync(
            id,
            UserStatuses.Locked,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Owner locked." : result.Error ?? "Could not lock owner.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id, CancellationToken cancellationToken)
    {
        var result = await _adminManagement.SetOwnerStatusAsync(
            id,
            UserStatuses.Active,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Owner unlocked." : result.Error ?? "Could not unlock owner.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
