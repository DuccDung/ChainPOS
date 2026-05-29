using ChainPOS.Constants;
using ChainPOS.Services.Inventory;
using ChainPOS.ViewModels.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class InventoryController : Controller
{
    private const string AreaName = "Owner";
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public async Task<IActionResult> Index(Guid? storeId, string? search, string? stockStatus, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetInventoryAsync(AreaName, storeId, search, stockStatus, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Import(Guid? storeId, Guid? productId, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetImportFormAsync(
            AreaName,
            new InventoryMovementViewModel { StoreId = storeId, ProductId = productId, Quantity = 1, MinQuantity = 5 },
            cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(InventoryMovementViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await _inventoryService.GetImportFormAsync(AreaName, model, cancellationToken));
        }

        var result = await _inventoryService.ImportStockAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not import stock.");
            return View(await _inventoryService.GetImportFormAsync(AreaName, model, cancellationToken));
        }

        TempData["SuccessMessage"] = "Stock imported.";
        return RedirectToAction(nameof(Index), new { storeId = model.StoreId });
    }

    public async Task<IActionResult> Export(Guid? storeId, Guid? productId, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetExportFormAsync(
            AreaName,
            new InventoryMovementViewModel { StoreId = storeId, ProductId = productId, Quantity = 1 },
            cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(InventoryMovementViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await _inventoryService.GetExportFormAsync(AreaName, model, cancellationToken));
        }

        var result = await _inventoryService.ExportStockAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not export stock.");
            return View(await _inventoryService.GetExportFormAsync(AreaName, model, cancellationToken));
        }

        TempData["SuccessMessage"] = "Stock exported.";
        return RedirectToAction(nameof(Index), new { storeId = model.StoreId });
    }

    public async Task<IActionResult> Adjust(Guid? storeId, Guid? productId, CancellationToken cancellationToken)
    {
        var model = await _inventoryService.GetAdjustFormAsync(
            AreaName,
            new InventoryAdjustViewModel { StoreId = storeId, ProductId = productId },
            cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(InventoryAdjustViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await _inventoryService.GetAdjustFormAsync(AreaName, model, cancellationToken));
        }

        var result = await _inventoryService.AdjustStockAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not adjust stock.");
            return View(await _inventoryService.GetAdjustFormAsync(AreaName, model, cancellationToken));
        }

        TempData["SuccessMessage"] = "Stock adjusted.";
        return RedirectToAction(nameof(Index), new { storeId = model.StoreId });
    }
}
