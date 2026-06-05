using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Common;
using ChainPOS.ViewModels.Staff.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Areas.Staff.Controllers;

[Area("Staff")]
[Authorize(Roles = AppRoles.Staff)]
public sealed class ProfileController : Controller
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ProfileController(StoreFlowDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.UserId) || !_currentUser.TenantId.HasValue)
        {
            return Forbid();
        }

        var tenantId = _currentUser.TenantId.Value;
        var userId = _currentUser.UserId;

        var profile = await _db.AspNetUsers
            .AsNoTracking()
            .Where(x => x.Id == userId
                && x.TenantId == tenantId
                && x.Roles.Any(r => r.Id == AppRoles.Staff))
            .Select(x => new StaffProfileViewModel
            {
                Id = x.Id,
                FullName = x.FullName ?? x.UserName ?? x.Email ?? x.Id,
                Email = x.Email ?? string.Empty,
                PhoneNumber = x.PhoneNumber,
                Status = x.Status,
                TenantName = x.Tenant != null ? x.Tenant.Name : string.Empty,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                LastLoginAt = x.LastLoginAt,
                Roles = x.Roles
                    .OrderBy(r => r.Name ?? r.Id)
                    .Select(r => r.Name ?? r.Id)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return NotFound();
        }

        profile.StoreAccesses = await _db.UserStores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId && !x.Store.IsDeleted)
            .OrderByDescending(x => x.IsActive && x.Store.Status == StoreStatuses.Active)
            .ThenBy(x => x.Store.Name)
            .Select(x => new StaffProfileStoreAccessViewModel
            {
                UserStoreId = x.Id,
                StoreId = x.StoreId,
                StoreName = x.Store.Name,
                StoreCode = x.Store.Code,
                StoreAddress = x.Store.Address,
                StorePhone = x.Store.Phone,
                StoreStatus = x.Store.Status,
                AssignmentIsActive = x.IsActive,
                StoreIsActive = x.Store.Status == StoreStatuses.Active,
                AssignedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return View(profile);
    }
}
