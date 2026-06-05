using ChainPOS.Constants;
using ChainPOS.Models;
using ChainPOS.Services.Audit;
using ChainPOS.ViewModels.Admin.Owners;
using ChainPOS.ViewModels.Admin.Tenants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Admin;

public sealed class AdminManagementService : IAdminManagementService
{
    private static readonly HashSet<string> ValidTenantStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        TenantStatuses.Active,
        TenantStatuses.Suspended,
        TenantStatuses.Cancelled,
        TenantStatuses.Trial
    };

    private readonly StoreFlowDbContext _db;
    private readonly PasswordHasher<AspNetUser> _passwordHasher;
    private readonly IAuditLogService _auditLog;

    public AdminManagementService(
        StoreFlowDbContext db,
        PasswordHasher<AspNetUser> passwordHasher,
        IAuditLogService auditLog)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _auditLog = auditLog;
    }

    public async Task<OwnerIndexViewModel> GetOwnersAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AspNetUsers
            .AsNoTracking()
            .Where(x => x.Roles.Any(r => r.Id == AppRoles.Owner));

        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(x =>
                (x.FullName != null && x.FullName.Contains(trimmedSearch)) ||
                (x.Email != null && x.Email.Contains(trimmedSearch)) ||
                (x.Tenant != null && x.Tenant.Name.Contains(trimmedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var owners = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new OwnerListItemViewModel
            {
                Id = x.Id,
                FullName = x.FullName ?? x.UserName ?? x.Email ?? x.Id,
                Email = x.Email ?? string.Empty,
                PhoneNumber = x.PhoneNumber,
                Status = x.Status,
                TenantName = x.Tenant != null ? x.Tenant.Name : string.Empty,
                TenantStatus = x.Tenant != null ? x.Tenant.Status : string.Empty,
                CreatedAt = x.CreatedAt,
                LastLoginAt = x.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        return new OwnerIndexViewModel
        {
            Search = trimmedSearch,
            Status = status,
            TotalOwners = owners.Count,
            Owners = owners
        };
    }

    public async Task<OwnerDetailsViewModel?> GetOwnerDetailsAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var owner = await _db.AspNetUsers
            .AsNoTracking()
            .Include(x => x.Tenant)
            .Where(x => x.Id == id && x.Roles.Any(r => r.Id == AppRoles.Owner))
            .FirstOrDefaultAsync(cancellationToken);

        if (owner is null)
        {
            return null;
        }

        var tenantId = owner.TenantId;
        var storeCount = 0;
        var staffCount = 0;
        var productCount = 0;
        var orderCount = 0;
        var revenueTotal = 0m;

        if (tenantId.HasValue)
        {
            storeCount = await _db.Stores.CountAsync(
                x => x.TenantId == tenantId.Value && !x.IsDeleted,
                cancellationToken);
            staffCount = await _db.AspNetUsers.CountAsync(
                x => x.TenantId == tenantId.Value && x.Roles.Any(r => r.Id == AppRoles.Staff),
                cancellationToken);
            productCount = await _db.Products.CountAsync(
                x => x.TenantId == tenantId.Value && !x.IsDeleted,
                cancellationToken);
            orderCount = await _db.Orders.CountAsync(
                x => x.TenantId == tenantId.Value,
                cancellationToken);
            revenueTotal = await _db.Orders
                .Where(x => x.TenantId == tenantId.Value && x.OrderStatus != OrderStatuses.Cancelled)
                .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;
        }

        return new OwnerDetailsViewModel
        {
            Id = owner.Id,
            FullName = owner.FullName ?? owner.UserName ?? owner.Email ?? owner.Id,
            Email = owner.Email ?? string.Empty,
            PhoneNumber = owner.PhoneNumber,
            Status = owner.Status,
            CreatedAt = owner.CreatedAt,
            LastLoginAt = owner.LastLoginAt,
            TenantId = owner.TenantId,
            TenantName = owner.Tenant?.Name ?? string.Empty,
            TenantStatus = owner.Tenant?.Status ?? string.Empty,
            TenantPhone = owner.Tenant?.Phone,
            TenantAddress = owner.Tenant?.Address,
            StoreCount = storeCount,
            StaffCount = staffCount,
            ProductCount = productCount,
            OrderCount = orderCount,
            RevenueTotal = revenueTotal
        };
    }

    public async Task<(bool Succeeded, string? Error)> CreateOwnerAsync(
        OwnerCreateViewModel model,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Normalize(model.Email);
        var normalizedPhone = NormalizePhone(model.PhoneNumber);
        var exists = await _db.AspNetUsers.AnyAsync(
            x => x.NormalizedEmail == normalizedEmail || x.NormalizedUserName == normalizedEmail,
            cancellationToken);
        if (exists)
        {
            return (false, "Email already exists.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            var phoneExists = await _db.AspNetUsers.AnyAsync(
                x => x.PhoneNumber == normalizedPhone,
                cancellationToken);
            if (phoneExists)
            {
                return (false, "Phone number already exists.");
            }
        }

        try
        {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var role = await EnsureRoleAsync(AppRoles.Owner, cancellationToken);
        var now = DateTime.UtcNow;
        var owner = new AspNetUser
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
            LockoutEnabled = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            CreatedAt = now,
            CreatedBy = currentUserId
        };
        owner.PasswordHash = _passwordHasher.HashPassword(owner, model.Password);
        owner.Roles.Add(role);

        _db.AspNetUsers.Add(owner);
        await _db.SaveChangesAsync(cancellationToken);

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = model.TenantName.Trim(),
            OwnerUserId = owner.Id,
            TaxCode = TrimToNull(model.TaxCode),
            Address = TrimToNull(model.TenantAddress),
            Phone = NormalizePhone(model.TenantPhone),
            Email = model.Email.Trim(),
            Status = TenantStatuses.Active,
            CreatedAt = now,
            CreatedBy = currentUserId
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);

        owner.TenantId = tenant.Id;
        owner.UpdatedAt = now;
        owner.UpdatedBy = currentUserId;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            "CreateUser",
            nameof(AspNetUser),
            owner.Id,
            newValue: $"Owner={owner.Email}; Tenant={tenant.Name}",
            tenantId: tenant.Id,
            cancellationToken: cancellationToken);
        await _auditLog.LogAsync(
            "CreateTenant",
            nameof(Tenant),
            tenant.Id.ToString(),
            newValue: $"Tenant={tenant.Name}; Owner={owner.Email}",
            tenantId: tenant.Id,
            cancellationToken: cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, "Email or phone number already exists.");
        }
    }

    public async Task<(bool Succeeded, string? Error)> SetOwnerStatusAsync(
        string id,
        string status,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Invalid owner status.");
        }

        var owner = await _db.AspNetUsers
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (owner is null || owner.Roles.All(x => x.Id != AppRoles.Owner))
        {
            return (false, "Owner not found.");
        }

        var oldStatus = owner.Status;
        owner.Status = status;
        owner.UpdatedAt = DateTime.UtcNow;
        owner.UpdatedBy = currentUserId;
        owner.SecurityStamp = Guid.NewGuid().ToString("N");

        if (string.Equals(status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase))
        {
            owner.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        }
        else
        {
            owner.LockoutEnd = null;
            owner.AccessFailedCount = 0;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            string.Equals(status, UserStatuses.Locked, StringComparison.OrdinalIgnoreCase) ? "LockUser" : "UnlockUser",
            nameof(AspNetUser),
            owner.Id,
            oldStatus,
            status,
            owner.TenantId,
            cancellationToken: cancellationToken);

        return (true, null);
    }

    public async Task<TenantIndexViewModel> GetTenantsAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Tenants
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        var trimmedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedSearch))
        {
            query = query.Where(x =>
                x.Name.Contains(trimmedSearch) ||
                (x.Email != null && x.Email.Contains(trimmedSearch)) ||
                (x.OwnerUser != null && x.OwnerUser.Email != null && x.OwnerUser.Email.Contains(trimmedSearch)) ||
                (x.OwnerUser != null && x.OwnerUser.FullName != null && x.OwnerUser.FullName.Contains(trimmedSearch)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var tenants = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TenantListItemViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Status = x.Status,
                OwnerName = x.OwnerUser != null ? x.OwnerUser.FullName ?? x.OwnerUser.UserName ?? string.Empty : string.Empty,
                OwnerEmail = x.OwnerUser != null ? x.OwnerUser.Email ?? string.Empty : string.Empty,
                StoreCount = x.Stores.Count(s => !s.IsDeleted),
                StaffCount = x.AspNetUsers.Count(u => u.Roles.Any(r => r.Id == AppRoles.Staff)),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new TenantIndexViewModel
        {
            Search = trimmedSearch,
            Status = status,
            TotalTenants = tenants.Count,
            Tenants = tenants
        };
    }

    public async Task<TenantDetailsViewModel?> GetTenantDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var storeCount = await _db.Stores.CountAsync(
            x => x.TenantId == id && !x.IsDeleted,
            cancellationToken);
        var staffCount = await _db.AspNetUsers.CountAsync(
            x => x.TenantId == id && x.Roles.Any(r => r.Id == AppRoles.Staff),
            cancellationToken);
        var productCount = await _db.Products.CountAsync(
            x => x.TenantId == id && !x.IsDeleted,
            cancellationToken);
        var orderCount = await _db.Orders.CountAsync(
            x => x.TenantId == id,
            cancellationToken);
        var revenueTotal = await _db.Orders
            .Where(x => x.TenantId == id && x.OrderStatus != OrderStatuses.Cancelled)
            .SumAsync(x => (decimal?)x.TotalAmount, cancellationToken) ?? 0m;

        return new TenantDetailsViewModel
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Status = tenant.Status,
            TaxCode = tenant.TaxCode,
            Address = tenant.Address,
            Phone = tenant.Phone,
            Email = tenant.Email,
            CreatedAt = tenant.CreatedAt,
            OwnerUserId = tenant.OwnerUserId,
            OwnerName = tenant.OwnerUser?.FullName ?? tenant.OwnerUser?.UserName ?? string.Empty,
            OwnerEmail = tenant.OwnerUser?.Email ?? string.Empty,
            OwnerStatus = tenant.OwnerUser?.Status ?? string.Empty,
            StoreCount = storeCount,
            StaffCount = staffCount,
            ProductCount = productCount,
            OrderCount = orderCount,
            RevenueTotal = revenueTotal
        };
    }

    public async Task<(bool Succeeded, string? Error)> SetTenantStatusAsync(
        Guid id,
        string status,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!ValidTenantStatuses.Contains(status))
        {
            return (false, "Invalid tenant status.");
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(
            x => x.Id == id && !x.IsDeleted,
            cancellationToken);
        if (tenant is null)
        {
            return (false, "Tenant not found.");
        }

        var oldStatus = tenant.Status;
        tenant.Status = status;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = currentUserId;

        await _db.SaveChangesAsync(cancellationToken);
        await _auditLog.LogAsync(
            status switch
            {
                TenantStatuses.Active => "ActivateTenant",
                TenantStatuses.Suspended => "SuspendTenant",
                TenantStatuses.Cancelled => "CancelTenant",
                _ => "ChangeTenantStatus"
            },
            nameof(Tenant),
            tenant.Id.ToString(),
            oldStatus,
            status,
            tenant.Id,
            cancellationToken: cancellationToken);

        return (true, null);
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
