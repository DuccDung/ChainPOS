using ChainPOS.Constants;
using ChainPOS.Services.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = AppRoles.Staff)]
public sealed class OrdersController : Controller
{
    private const string AreaName = "Staff";
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<IActionResult> Index(
        Guid? storeId,
        string? search,
        string? status,
        string? paymentStatus,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var model = await _orderService.GetOrdersAsync(AreaName, storeId, search, status, paymentStatus, date, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await _orderService.GetOrderDetailsAsync(AreaName, id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _orderService.CancelOrderAsync(id, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Order cancelled." : result.Error ?? "Could not cancel order.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
