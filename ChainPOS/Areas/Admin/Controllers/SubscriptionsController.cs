using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Services.Subscriptions;
using ChainPOS.ViewModels.Admin.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class SubscriptionsController : Controller
{
    private readonly ISubscriptionManagementService _subscriptionService;

    public SubscriptionsController(ISubscriptionManagementService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public async Task<IActionResult> Create(Guid? tenantId, CancellationToken cancellationToken)
    {
        var model = await _subscriptionService.GetTenantSubscriptionCreateAsync(tenantId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TenantSubscriptionCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var optionsModel = await _subscriptionService.GetTenantSubscriptionCreateAsync(model.TenantId, cancellationToken);
            model.Tenants = optionsModel.Tenants;
            model.Plans = optionsModel.Plans;
            return View(model);
        }

        var result = await _subscriptionService.CreateTenantSubscriptionAsync(
            model,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);
        if (!result.Succeeded)
        {
            var optionsModel = await _subscriptionService.GetTenantSubscriptionCreateAsync(model.TenantId, cancellationToken);
            model.Tenants = optionsModel.Tenants;
            model.Plans = optionsModel.Plans;
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create subscription.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Tenant subscription created.";
        return model.TenantId.HasValue
            ? RedirectToAction("Details", "Tenants", new { id = model.TenantId.Value })
            : RedirectToAction("Index", "Tenants");
    }
}
