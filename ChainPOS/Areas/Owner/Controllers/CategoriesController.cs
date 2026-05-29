using ChainPOS.Constants;
using ChainPOS.Services.Owner;
using ChainPOS.ViewModels.Owner.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Roles = AppRoles.Owner)]
public sealed class CategoriesController : Controller
{
    private readonly IOwnerCategoryService _categoryService;

    public CategoriesController(IOwnerCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<IActionResult> Index(string? search, string? status, CancellationToken cancellationToken)
    {
        var model = await _categoryService.GetCategoriesAsync(search, status, cancellationToken);
        return View(model);
    }

    public IActionResult Create()
    {
        return View(new CategoryFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _categoryService.CreateCategoryAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create category.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Category created.";
        return RedirectToAction(nameof(Details), new { id = result.CategoryId });
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var model = await _categoryService.GetCategoryFormAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CategoryFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Id = id;
            return View(model);
        }

        var result = await _categoryService.UpdateCategoryAsync(id, model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not update category.");
            model.Id = id;
            return View(model);
        }

        TempData["SuccessMessage"] = "Category updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await _categoryService.GetCategoryDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
        => ToggleAsync(id, true, "Category activated.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
        => ToggleAsync(id, false, "Category deactivated.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _categoryService.DeleteCategoryAsync(id, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Category deleted." : result.Error ?? "Could not delete category.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ToggleAsync(
        Guid id,
        bool isActive,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.ToggleCategoryAsync(id, isActive, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error ?? "Could not update category.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
