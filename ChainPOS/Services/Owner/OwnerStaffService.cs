using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.Services.Common;
using ChainPOS.ViewModels.Owner.Staff;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Owner;

public sealed class OwnerStaffService : IOwnerStaffService
{
    private readonly StoreFlowDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly PasswordHasher<AspNetUser> _passwordHasher;
    private readonly IAuditLogService _auditLog;

    public OwnerStaffService(
        StoreFlowDbContext db,
        ICurrentUserService currentUser,
        PasswordHasher<AspNetUser> passwordHasher,
        IAuditLogService auditLog)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _auditLog = auditLog;
    }

    public async Task<StaffIndexViewModel> GetStaffAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var query = _db.AspNetUsers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Roles.Any(r => r.Id == AppRoles.Staff));

        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(x =>
                (x.FullName != null && x.FullName.Contains(trimmedSearch)) ||
                (x.Email != null && x.Email.Contains(trimmedSearch)) ||
                (x.PhoneNumber != null && x.PhoneNumber.Contains(trimmedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var staff = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new StaffListItemViewModel
            {
                Id = x.Id,
                FullName = x.FullName ?? x.UserName ?? x.Email ?? x.Id,
                Email = x.Email ?? string.Empty,
                PhoneNumber = x.PhoneNumber,
                Status = x.Status,
                AssignedStoreCount = x.UserStores.Count(s => s.TenantId == tenantId),
                ActiveStoreCount = x.UserStores.Count(s => s.TenantId == tenantId && s.IsActive),
                CreatedAt = x.CreatedAt,
                LastLoginAt = x.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        var baseQuery = _db.AspNetUsers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Roles.Any(r => r.Id == AppRoles.Staff));

        return new StaffIndexViewModel
        {
            Search = trimmedSearch,
            Status = status,
            TotalStaff = await baseQuery.CountAsync(cancellationToken),
            ActiveStaff = await baseQuery.CountAsync(x => x.Status == UserStatuses.Active, cancellationToken),
            LockedStaff = await baseQuery.CountAsync(x => x.Status == UserStatuses.Locked, cancellationToken),
            AssignedStaff = await baseQuery.CountAsync(x => x.UserStores.Any(s => s.TenantId == tenantId && s.IsActive), cancellationToken),
            MaxStaff = await GetMaxStaffAsync(tenantId, cancellationToken),
            Staff = staff
        };
    }

    public async Task<StaffCreateViewModel> GetCreateFormAsync(
        StaffCreateViewModel? model = null,
        CancellationToken cancellationToken = default)
    {
        model ??= new StaffCreateViewModel();
        model.Stores = await GetStoreOptionsAsync(cancellationToken);
        return model;
    }

    public async Task<(bool Succeeded, string? Error, string? StaffId)> CreateStaffAsync(
        StaffCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var normalizedEmail = Normalize(model.Email);
        var normalizedPhone = NormalizePhone(model.PhoneNumber);
        var exists = await _db.AspNetUsers.AnyAsync(
            x => x.NormalizedEmail == normalizedEmail || x.NormalizedUserName == normalizedEmail,
            cancellationToken);
        if (exists)
        {
            return (false, "Email already exists.", null);
        }

        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            var phoneExists = await _db.AspNetUsers.AnyAsync(
                x => x.PhoneNumber == normalizedPhone,
                cancellationToken);
            if (phoneExists)
            {
                return (false, "Phone number already exists.", null);
            }
        }

        var maxStaff = await GetMaxStaffAsync(tenantId, cancellationToken);
        if (maxStaff.HasValue)
        {
            var currentStaffCount = await _db.AspNetUsers.CountAsync(
                x => x.TenantId == tenantId && x.Roles.Any(r => r.Id == AppRoles.Staff),
                cancellationToken);
            if (currentStaffCount >= maxStaff.Value)
            {
                return (false, $"Staff limit reached for current subscription plan ({maxStaff.Value}).", null);
            }
        }

        var validStoreIds = await _db.Stores
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && model.StoreIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (validStoreIds.Count != model.StoreIds.Distinct().Count())
        {
            return (false, "One or more selected stores are invalid.", null);
        }

        try
        {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var role = await EnsureRoleAsync(AppRoles.Staff, cancellationToken);
        var now = DateTime.UtcNow;
        var staff = new AspNetUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = model.Email.Trim(),
            NormalizedUserName = normalizedEmail,
            Email = model.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            PhoneNumber = normalizedPhone,
            FullName = model.FullName.Trim(),
            Status = UserStatuses.Active,
            TenantId = tenantId,
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            CreatedBy = _currentUser.UserId
        };
        staff.PasswordHash = _passwordHasher.HashPassword(staff, model.Password);
        staff.Roles.Add(role);

        _db.AspNetUsers.Add(staff);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var storeId in validStoreIds.Distinct())
        {
            _db.UserStores.Add(new UserStore
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = staff.Id,
                StoreId = storeId,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = _currentUser.UserId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "CreateStaff",
            nameof(AspNetUser),
            staff.Id,
            newValue: $"Staff={staff.Email}; Stores={validStoreIds.Count}",
            tenantId: tenantId,
            cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return (true, null, staff.Id);
        }
        catch (DbUpdateException)
        {
            return (false, "Email or phone number already exists.", null);
        }
    }

    public async Task<StaffDetailsViewModel?> GetStaffDetailsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var staff = await _db.AspNetUsers
            .AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId && x.Roles.Any(r => r.Id == AppRoles.Staff))
            .Select(x => new StaffDetailsViewModel
            {
                Id = x.Id,
                FullName = x.FullName ?? x.UserName ?? x.Email ?? x.Id,
                Email = x.Email ?? string.Empty,
                PhoneNumber = x.PhoneNumber,
                Status = x.Status,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                LastLoginAt = x.LastLoginAt,
                StoreAssignments = x.UserStores
                    .Where(s => s.TenantId == tenantId)
                    .OrderBy(s => s.Store.Name)
                    .Select(s => new StaffStoreAssignmentViewModel
                    {
                        UserStoreId = s.Id,
                        StoreId = s.StoreId,
                        StoreName = s.Store.Name,
                        StoreCode = s.Store.Code,
                        IsActive = s.IsActive,
                        CreatedAt = s.CreatedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (staff is null)
        {
            return null;
        }

        var assignedStoreIds = staff.StoreAssignments.Select(x => x.StoreId).ToHashSet();
        staff.AvailableStores = await _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && !assignedStoreIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new StaffStoreOptionViewModel
            {
                StoreId = x.Id,
                StoreName = x.Name,
                StoreCode = x.Code
            })
            .ToListAsync(cancellationToken);

        return staff;
    }

    public async Task<StaffResetPasswordViewModel?> GetResetPasswordFormAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        return await _db.AspNetUsers
            .AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == tenantId && x.Roles.Any(r => r.Id == AppRoles.Staff))
            .Select(x => new StaffResetPasswordViewModel
            {
                StaffId = x.Id,
                StaffName = x.FullName ?? x.UserName ?? x.Email ?? x.Id,
                Email = x.Email ?? string.Empty
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Succeeded, string? Error)> ResetPasswordAsync(
        string id,
        StaffResetPasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var staff = await LoadStaffForUpdateAsync(id, tenantId, cancellationToken);
        if (staff is null)
        {
            return (false, "Staff not found.");
        }

        staff.PasswordHash = _passwordHasher.HashPassword(staff, model.Password);
        staff.SecurityStamp = Guid.NewGuid().ToString("N");
        staff.UpdatedAt = DateTime.UtcNow;
        staff.UpdatedBy = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "ResetStaffPassword",
            nameof(AspNetUser),
            staff.Id,
            newValue: $"Staff={staff.Email}",
            tenantId: tenantId,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SetStaffStatusAsync(
        string id,
        string status,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Invalid staff status.");
        }

        var tenantId = RequireTenantId();
        var staff = await LoadStaffForUpdateAsync(id, tenantId, cancellationToken);
        if (staff is null)
        {
            return (false, "Staff not found.");
        }

        var oldStatus = staff.Status;
        staff.Status = status;
        staff.UpdatedAt = DateTime.UtcNow;
        staff.UpdatedBy = _currentUser.UserId;
        staff.SecurityStamp = Guid.NewGuid().ToString("N");
        if (string.Equals(status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase))
        {
            staff.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        }
        else
        {
            staff.LockoutEnd = null;
            staff.AccessFailedCount = 0;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            string.Equals(status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase) ? "LockStaff" : "UnlockStaff",
            nameof(AspNetUser),
            staff.Id,
            oldStatus,
            status,
            tenantId,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> AssignStoreAsync(
        string staffId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var staffExists = await _db.AspNetUsers.AnyAsync(
            x => x.Id == staffId && x.TenantId == tenantId && x.Roles.Any(r => r.Id == AppRoles.Staff),
            cancellationToken);
        if (!staffExists)
        {
            return (false, "Staff not found.");
        }

        var storeExists = await _db.Stores.AnyAsync(
            x => x.Id == storeId && x.TenantId == tenantId && !x.IsDeleted,
            cancellationToken);
        if (!storeExists)
        {
            return (false, "Store not found.");
        }

        var existing = await _db.UserStores.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserId == staffId && x.StoreId == storeId,
            cancellationToken);
        if (existing is not null)
        {
            existing.IsActive = true;
            await _db.SaveChangesAsync(cancellationToken);
            return (true, null);
        }

        _db.UserStores.Add(new UserStore
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = staffId,
            StoreId = storeId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        });
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "AssignStaffStore",
            nameof(UserStore),
            staffId,
            newValue: $"StoreId={storeId}",
            tenantId: tenantId,
            storeId: storeId,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<(bool Succeeded, string? Error)> SetStoreAssignmentStatusAsync(
        string staffId,
        Guid userStoreId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var userStore = await _db.UserStores
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.Id == userStoreId
                    && x.UserId == staffId
                    && x.TenantId == tenantId
                    && x.User.Roles.Any(r => r.Id == AppRoles.Staff),
                cancellationToken);
        if (userStore is null)
        {
            return (false, "Store assignment not found.");
        }

        var oldValue = userStore.IsActive.ToString();
        userStore.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            isActive ? "EnableStaffStore" : "DisableStaffStore",
            nameof(UserStore),
            userStore.Id.ToString(),
            oldValue,
            isActive.ToString(),
            tenantId,
            userStore.StoreId,
            cancellationToken);

        return (true, null);
    }

    private async Task<AspNetUser?> LoadStaffForUpdateAsync(
        string id,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _db.AspNetUsers
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(
                x => x.Id == id
                    && x.TenantId == tenantId
                    && x.Roles.Any(r => r.Id == AppRoles.Staff),
                cancellationToken);
    }

    private async Task<IReadOnlyList<StaffStoreOptionViewModel>> GetStoreOptionsAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        return await _db.Stores
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new StaffStoreOptionViewModel
            {
                StoreId = x.Id,
                StoreName = x.Name,
                StoreCode = x.Code
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<AspNetRole> EnsureRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var role = await _db.AspNetRoles.FirstOrDefaultAsync(x => x.Id == roleName, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new AspNetRole
        {
            Id = roleName,
            Name = roleName,
            NormalizedName = Normalize(roleName),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        _db.AspNetRoles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);

        return role;
    }

    private async Task<int?> GetMaxStaffAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _db.TenantSubscriptions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.Status == "Active"
                && (x.EndDate == null || x.EndDate >= today)
                && x.Plan.IsActive
                && !x.Plan.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Plan.MaxStaff)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Guid RequireTenantId()
    {
        if (!_currentUser.TenantId.HasValue)
        {
            throw new InvalidOperationException("Current owner does not have a tenant.");
        }

        return _currentUser.TenantId.Value;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = new string(value.Where(c => char.IsDigit(c) || c == '+').ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
