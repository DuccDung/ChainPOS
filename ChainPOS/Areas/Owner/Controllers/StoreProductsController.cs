using ChainPOS.Constants;
using ChainPOS.Services.Owner;
using ChainPOS.ViewModels.Owner.StoreProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class StoreProductsController : Controller
{
    private readonly IOwnerStoreProductService _storeProductService;

    public StoreProductsController(IOwnerStoreProductService storeProductService)
    {
        _storeProductService = storeProductService;
    }

    public async Task<IActionResult> Index(
        Guid? storeId,
        string? search,
        string? availability,
        CancellationToken cancellationToken)
    {
        var model = await _storeProductService.GetStoreProductsAsync(
            storeId,
            search,
            availability,
            cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Assign(Guid? storeId, Guid? productId, CancellationToken cancellationToken)
    {
        var model = await _storeProductService.GetAssignFormAsync(
            new StoreProductAssignViewModel
            {
                StoreId = storeId,
                ProductId = productId
            },
            cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(StoreProductAssignViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await _storeProductService.GetAssignFormAsync(model, cancellationToken));
        }

        var result = await _storeProductService.AssignProductAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not assign product to store.");
            return View(await _storeProductService.GetAssignFormAsync(model, cancellationToken));
        }

        TempData["SuccessMessage"] = "Product assigned to store.";
        return RedirectToAction(nameof(Index), new { storeId = model.StoreId });
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = await _storeProductService.GetEditFormAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        StoreProductEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        var result = await _storeProductService.UpdateStoreProductAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not update store product.");
            model.Id = id;
            return View(model);
        }

        TempData["SuccessMessage"] = "Store product updated.";
        return RedirectToAction(nameof(Index), new { storeId = model.StoreId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Enable(Guid id, Guid? storeId, CancellationToken cancellationToken)
        => SetAvailabilityAsync(id, storeId, true, "Product enabled for store.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Disable(Guid id, Guid? storeId, CancellationToken cancellationToken)
        => SetAvailabilityAsync(id, storeId, false, "Product disabled for store.", cancellationToken);

    private async Task<IActionResult> SetAvailabilityAsync(
        Guid id,
        Guid? storeId,
        bool isAvailable,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var result = await _storeProductService.SetAvailabilityAsync(id, isAvailable, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error ?? "Could not update store product.";

        return RedirectToAction(nameof(Index), new { storeId });
    }
}
