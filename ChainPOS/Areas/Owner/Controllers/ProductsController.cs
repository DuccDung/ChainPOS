using ChainPOS.Constants;
using ChainPOS.Services.Owner;
using ChainPOS.ViewModels.Owner.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class ProductsController : Controller
{
    private readonly IOwnerProductService _productService;

    public ProductsController(IOwnerProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index(
        string? search,
        Guid? categoryId,
        string? status,
        CancellationToken cancellationToken)
    {
        var model = await _productService.GetProductsAsync(search, categoryId, status, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var model = await _productService.GetCreateFormAsync(cancellationToken: cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(await _productService.GetCreateFormAsync(model, cancellationToken));
        }

        var result = await _productService.CreateProductAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create product.");
            return View(await _productService.GetCreateFormAsync(model, cancellationToken));
        }

        TempData["SuccessMessage"] = "Product created.";
        return RedirectToAction(nameof(Details), new { id = result.ProductId });
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = await _productService.GetProductFormAsync(id, cancellationToken);
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
        ProductFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(await _productService.GetCreateFormAsync(model, cancellationToken));
        }

        var result = await _productService.UpdateProductAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not update product.");
            model.Id = id;
            return View(await _productService.GetCreateFormAsync(model, cancellationToken));
        }

        TempData["SuccessMessage"] = "Product updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await _productService.GetProductDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
        => ToggleAsync(id, true, "Product activated.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
        => ToggleAsync(id, false, "Product deactivated.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _productService.DeleteProductAsync(id, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Product deleted." : result.Error ?? "Could not delete product.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ToggleAsync(
        Guid id,
        bool isActive,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var result = await _productService.ToggleProductAsync(id, isActive, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error ?? "Could not update product.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
