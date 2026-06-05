using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Services.Import;
using ChainPOS.Services.Admin;
using ChainPOS.ViewModels.Admin.Owners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class OwnersController : Controller
{
    private readonly IAdminManagementService _adminManagement;
    private readonly IBulkImportService _bulkImport;

    public OwnersController(IAdminManagementService adminManagement, IBulkImportService bulkImport)
    {
        _adminManagement = adminManagement;
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

        var result = await _bulkImport.ImportOwnersAsync(
            file,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);
        return View("~/Views/Shared/Imports/Result.cshtml", result);
    }

    public IActionResult Template()
    {
        const string csv = "FullName,Email,PhoneNumber,TenantName,TaxCode,TenantAddress,TenantPhone,Password\nNguyen Van A,owner.new@demo.local,0909000001,Demo Tenant,0312340000,Demo address,0909000001,Owner@123\n";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "owners-template.csv");
    }

    public async Task<IActionResult> Index(string? search, string? status, CancellationToken cancellationToken)
    {
        var model = await _adminManagement.GetOwnersAsync(search, status, cancellationToken);
        return View(model);
    }

    public IActionResult Create()
    {
        return View(new OwnerCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OwnerCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _adminManagement.CreateOwnerAsync(
            model,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create owner.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Owner and tenant created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(string id, CancellationToken cancellationToken)
    {
        var model = await _adminManagement.GetOwnerDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(string id, CancellationToken cancellationToken)
    {
        var result = await _adminManagement.SetOwnerStatusAsync(
            id,
            UserStatuses.Locked,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Owner locked." : result.Error ?? "Could not lock owner.";

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id, CancellationToken cancellationToken)
    {
        var result = await _adminManagement.SetOwnerStatusAsync(
            id,
            UserStatuses.Active,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? "Owner unlocked." : result.Error ?? "Could not unlock owner.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
