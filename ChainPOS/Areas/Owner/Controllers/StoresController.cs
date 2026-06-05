using ChainPOS.Constants;
using ChainPOS.Services.Import;
using ChainPOS.Services.Owner;
using ChainPOS.ViewModels.Owner.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class StoresController : Controller
{
    private readonly IOwnerStoreService _storeService;
    private readonly IBulkImportService _bulkImport;

    public StoresController(IOwnerStoreService storeService, IBulkImportService bulkImport)
    {
        _storeService = storeService;
        _bulkImport = bulkImport;
    }

    public IActionResult Import()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Import file is required.";
            return View();
        }

        var result = await _bulkImport.ImportStoresAsync(file, cancellationToken);
        return View("~/Views/Shared/Imports/Result.cshtml", result);
    }

    public IActionResult Template()
    {
        const string csv = "Name,Code,Address,Phone,Status\nDemo Store,DEMO-STORE-02,Demo address,0909000003,Active\n";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "stores-template.csv");
    }

    public async Task<IActionResult> Index(string? search, string? status, CancellationToken cancellationToken)
    {
        var model = await _storeService.GetStoresAsync(search, status, cancellationToken);
        return View(model);
    }

    public IActionResult Create()
    {
        return View(new StoreFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StoreFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _storeService.CreateStoreAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create store.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Store created.";
        return RedirectToAction(nameof(Details), new { id = result.StoreId });
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = await _storeService.GetStoreFormAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, StoreFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        var result = await _storeService.UpdateStoreAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not update store.");
            model.Id = id;
            return View(model);
        }

        TempData["SuccessMessage"] = "Store updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await _storeService.GetStoreDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, StoreStatuses.Active, "Store activated.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, StoreStatuses.Inactive, "Store deactivated.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, StoreStatuses.Closed, "Store closed.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _storeService.DeleteStoreAsync(id, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Store deleted." : result.Error ?? "Could not delete store.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ChangeStatusAsync(
        Guid id,
        string status,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var result = await _storeService.SetStoreStatusAsync(id, status, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error ?? "Could not update store status.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
