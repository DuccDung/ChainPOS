using ChainPOS.Constants;
using ChainPOS.Services.Sales;
using ChainPOS.ViewModels.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class ShiftsController : Controller
{
    private const string AreaName = "Owner";
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    public async Task<IActionResult> Index(Guid? storeId, string? status, CancellationToken cancellationToken)
    {
        var model = await _shiftService.GetShiftsAsync(AreaName, storeId, status, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Open(Guid? storeId, CancellationToken cancellationToken)
    {
        var model = await _shiftService.GetOpenFormAsync(
            AreaName,
            new ShiftOpenViewModel { StoreId = storeId },
            cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(ShiftOpenViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await _shiftService.GetOpenFormAsync(AreaName, model, cancellationToken));
        }

        var result = await _shiftService.OpenShiftAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not open shift.");
            return View(await _shiftService.GetOpenFormAsync(AreaName, model, cancellationToken));
        }

        TempData["SuccessMessage"] = "Shift opened.";
        return RedirectToAction(nameof(Index), new { storeId = model.StoreId, status = ShiftStatuses.Open });
    }

    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        var model = await _shiftService.GetCloseFormAsync(AreaName, id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(Guid id, ShiftCloseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var form = await _shiftService.GetCloseFormAsync(AreaName, id, cancellationToken);
            return form is null ? NotFound() : View(form);
        }

        var result = await _shiftService.CloseShiftAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not close shift.");
            var form = await _shiftService.GetCloseFormAsync(AreaName, id, cancellationToken);
            return form is null ? NotFound() : View(form);
        }

        TempData["SuccessMessage"] = "Shift closed.";
        return RedirectToAction(nameof(Index));
    }
}
