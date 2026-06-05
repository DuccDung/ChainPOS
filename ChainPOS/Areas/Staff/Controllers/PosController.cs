using ChainPOS.Constants;
using ChainPOS.Services.Sales;
using ChainPOS.ViewModels.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = AppRoles.Staff)]
public sealed class PosController : Controller
{
    private const string AreaName = "Staff";
    private readonly IPosService _posService;

    public PosController(IPosService posService)
    {
        _posService = posService;
    }

    public async Task<IActionResult> Index(Guid? storeId, string? search, CancellationToken cancellationToken)
    {
        var model = await _posService.GetRegisterAsync(AreaName, storeId, search, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(PosCheckoutInputModel model, CancellationToken cancellationToken)
    {
        var result = await _posService.CheckoutAsync(model, cancellationToken);
        if (!result.Succeeded || !result.OrderId.HasValue)
        {
            TempData["ErrorMessage"] = result.Error ?? "Could not checkout.";
            return RedirectToAction(nameof(Index), new { storeId = model.StoreId });
        }

        TempData["SuccessMessage"] = "Order created.";
        return RedirectToAction("Details", "Orders", new { area = AreaName, id = result.OrderId.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteQueuedOrder(Guid id, Guid? storeId, CancellationToken cancellationToken)
    {
        var result = await _posService.CompletePendingOrderAsync(id, cancellationToken);
        if (!result.Succeeded || !result.OrderId.HasValue)
        {
            TempData["ErrorMessage"] = result.Error ?? "Could not complete queued order.";
            return RedirectToAction(nameof(Index), new { storeId });
        }

        TempData["SuccessMessage"] = "Queued order completed.";
        return RedirectToAction("Details", "Orders", new { area = AreaName, id = result.OrderId.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelQueuedOrder(Guid id, Guid? storeId, CancellationToken cancellationToken)
    {
        var result = await _posService.CancelPendingOrderAsync(id, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Queued order cancelled." : result.Error ?? "Could not cancel queued order.";

        return RedirectToAction(nameof(Index), new { storeId });
    }
}
