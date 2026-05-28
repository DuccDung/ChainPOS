using System.Security.Claims;
using ChainPOS.Constants;
using ChainPOS.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class TenantsController : Controller
{
    private readonly IAdminManagementService _adminManagement;

    public TenantsController(IAdminManagementService adminManagement)
    {
        _adminManagement = adminManagement;
    }

    public async Task<IActionResult> Index(string? search, string? status, CancellationToken cancellationToken)
    {
        var model = await _adminManagement.GetTenantsAsync(search, status, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await _adminManagement.GetTenantDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, TenantStatuses.Active, "Tenant activated.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, TenantStatuses.Suspended, "Tenant suspended.", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        => ChangeStatusAsync(id, TenantStatuses.Cancelled, "Tenant cancelled.", cancellationToken);

    private async Task<IActionResult> ChangeStatusAsync(
        Guid id,
        string status,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var result = await _adminManagement.SetTenantStatusAsync(
            id,
            status,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded ? successMessage : result.Error ?? "Could not update tenant.";

        return RedirectToAction(nameof(Details), new { id });
    }
}
