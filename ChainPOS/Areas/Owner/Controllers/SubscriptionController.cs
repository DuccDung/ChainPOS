using ChainPOS.Constants;
using ChainPOS.Services.Common;
using ChainPOS.Services.Payments;
using ChainPOS.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class SubscriptionController : Controller
{
    private readonly ISubscriptionManagementService _subscriptionService;
    private readonly ISystemPaymentSePayService _sePayService;
    private readonly ICurrentUserService _currentUser;

    public SubscriptionController(
        ISubscriptionManagementService subscriptionService,
        ISystemPaymentSePayService sePayService,
        ICurrentUserService currentUser)
    {
        _subscriptionService = subscriptionService;
        _sePayService = sePayService;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _subscriptionService.GetOwnerSubscriptionAsync(cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Pay(Guid id, CancellationToken cancellationToken)
    {
        var model = await _sePayService.BuildCheckoutPageAsync(
            id,
            _currentUser.TenantId,
            allowAnyTenant: false,
            cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        model.LayoutPath = "~/Views/Shared/_OwnerLayout.cshtml";
        model.BackUrl = Url.Action(nameof(Index), "Subscription", new { area = "Owner" }) ?? "/Owner/Subscription";
        model.BackLabel = "Back to subscription";
        return View("~/Views/Shared/SystemPayments/Checkout.cshtml", model);
    }
}
