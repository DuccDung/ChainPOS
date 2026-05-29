using ChainPOS.Constants;
using ChainPOS.Services.Owner;
using ChainPOS.ViewModels.Owner.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class StaffController : Controller
{
    private readonly IOwnerStaffService _staffService;

    public StaffController(IOwnerStaffService staffService)
    {
        _staffService = staffService;
    }

    public async Task<IActionResult> Index(string? search, string? status, CancellationToken cancellationToken)
    {
        var model = await _staffService.GetStaffAsync(search, status, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await _staffService.GetCreateFormAsync(cancellationToken: cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await _staffService.GetCreateFormAsync(model, cancellationToken));
        }

        var result = await _staffService.CreateStaffAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create staff.");
            return View(await _staffService.GetCreateFormAsync(model, cancellationToken));
        }

        TempData["SuccessMessage"] = "Staff created.";
        return RedirectToAction(nameof(Details), new { id = result.StaffId });
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var model = await _staffService.GetStaffDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    public async Task<IActionResult> ResetPassword(string id, CancellationToken cancellationToken)
    {
        var model = await _staffService.GetResetPasswordFormAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, StaffResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.StaffId = id;
            return View(model);
        }

        var result = await _staffService.ResetPasswordAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not reset password.");
            model.StaffId = id;
            return View(model);
        }

        TempData["SuccessMessage"] = "Staff password reset.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Lock(string id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, UserStatuses.Locked, "Staff locked.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Unlock(string id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, UserStatuses.Active, "Staff unlocked.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignStore(string id, Guid storeId, CancellationToken cancellationToken)
    {
        var result = await _staffService.AssignStoreAsync(id, storeId, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Store assigned." : result.Error ?? "Could not assign store.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableStore(string id, Guid userStoreId, CancellationToken cancellationToken)
    {
        var result = await _staffService.SetStoreAssignmentStatusAsync(id, userStoreId, true, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Store access enabled." : result.Error ?? "Could not enable store access.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableStore(string id, Guid userStoreId, CancellationToken cancellationToken)
    {
        var result = await _staffService.SetStoreAssignmentStatusAsync(id, userStoreId, false, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Store access disabled." : result.Error ?? "Could not disable store access.";

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IActionResult> ChangeStatusAsync(
        string id,
        string status,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var result = await _staffService.SetStaffStatusAsync(id, status, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error ?? "Could not update staff status.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
