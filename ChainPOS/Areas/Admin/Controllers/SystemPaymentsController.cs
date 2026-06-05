using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Services.Payments;
using ChainPOS.Services.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class SystemPaymentsController : Controller
{
    private readonly ISubscriptionManagementService _subscriptionService;
    private readonly ISystemPaymentSePayService _sePayService;

    public SystemPaymentsController(
        ISubscriptionManagementService subscriptionService,
        ISystemPaymentSePayService sePayService)
    {
        _subscriptionService = subscriptionService;
        _sePayService = sePayService;
    }

    public async Task<IActionResult> Index(Guid? tenantId, string? status, string? search, CancellationToken cancellationToken)
    {
        var model = await _subscriptionService.GetSystemPaymentsAsync(tenantId, status, search, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await _sePayService.BuildCheckoutPageAsync(
            id,
            tenantId: null,
            allowAnyTenant: true,
            cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        model.LayoutPath = "~/Views/Shared/_AdminLayout.cshtml";
        model.BackUrl = Url.Action(nameof(Index), "SystemPayments", new { area = "Admin" }) ?? "/Admin/SystemPayments";
        model.BackLabel = "Back to system payments";
        return View("~/Views/Shared/SystemPayments/Checkout.cshtml", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkPaid(Guid id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, true, "System payment marked as paid.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkFailed(Guid id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, false, "System payment marked as failed.", cancellationToken);

    private async Task<IActionResult> ChangeStatusAsync(Guid id, bool paid, string successMessage, CancellationToken cancellationToken)
    {
        var result = paid
            ? await _subscriptionService.MarkSystemPaymentPaidAsync(id, CurrentUserId(), cancellationToken)
            : await _subscriptionService.MarkSystemPaymentFailedAsync(id, CurrentUserId(), cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error ?? "Could not update payment.";

        return RedirectToAction(nameof(Index));
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
