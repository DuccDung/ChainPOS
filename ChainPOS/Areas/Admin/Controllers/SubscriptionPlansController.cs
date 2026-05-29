using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Services.Subscriptions;
using ChainPOS.ViewModels.Admin.SubscriptionPlans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class SubscriptionPlansController : Controller
{
    private readonly ISubscriptionManagementService _subscriptionService;

    public SubscriptionPlansController(ISubscriptionManagementService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public async Task<IActionResult> Index(string? search, string? status, CancellationToken cancellationToken)
    {
        var model = await _subscriptionService.GetPlansAsync(search, status, cancellationToken);
        return View(model);
    }

    public IActionResult Create()
    {
        return View(new SubscriptionPlanFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubscriptionPlanFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _subscriptionService.CreatePlanAsync(model, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create plan.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Subscription plan created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = await _subscriptionService.GetPlanFormAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, SubscriptionPlanFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        var result = await _subscriptionService.UpdatePlanAsync(id, model, CurrentUserId(), cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not update plan.");
            model.Id = id;
            return View(model);
        }

        TempData["SuccessMessage"] = "Subscription plan updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
        => SetActiveAsync(id, true, "Plan activated.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
        => SetActiveAsync(id, false, "Plan deactivated.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.DeletePlanAsync(id, CurrentUserId(), cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Plan deleted." : result.Error ?? "Could not delete plan.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> SetActiveAsync(Guid id, bool isActive, string successMessage, CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.SetPlanActiveAsync(id, isActive, CurrentUserId(), cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error ?? "Could not update plan.";
        return RedirectToAction(nameof(Index));
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
