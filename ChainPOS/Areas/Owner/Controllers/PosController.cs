using ChainPOS.Constants;
using ChainPOS.Services.Sales;
using ChainPOS.ViewModels.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class PosController : Controller
{
    private const string AreaName = "Owner";
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
}
