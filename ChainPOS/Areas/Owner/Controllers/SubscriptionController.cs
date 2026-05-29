using ChainPOS.Constants;
using ChainPOS.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class SubscriptionController : Controller
{
    private readonly ISubscriptionManagementService _subscriptionService;

    public SubscriptionController(ISubscriptionManagementService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _subscriptionService.GetOwnerSubscriptionAsync(cancellationToken);
        return View(model);
    }
}
