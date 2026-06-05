using ChainPOS.Constants;
using ChainPOS.Services.Sales;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class PaymentsController : Controller
{
    private const string AreaName = "Owner";
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    public async Task<IActionResult> Index(
        Guid? storeId,
        string? search,
        string? method,
        string? status,
        DateOnly? date,
        CancellationToken cancellationToken)
    {
        var model = await _paymentService.GetPaymentsAsync(
            AreaName,
            storeId,
            search,
            method,
            status,
            date,
            cancellationToken);
        return View(model);
    }
}
