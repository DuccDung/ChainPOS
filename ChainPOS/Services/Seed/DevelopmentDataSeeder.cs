using ChainPOS.Constants;
using ChainPOS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChainPOS.Services.Seed;

public static class DevelopmentDataSeeder
{
    private const string DefaultAdminEmail = "admin@chainpos.local";
    private const string DefaultAdminPassword = "Admin@123";
    private const string DefaultOwnerEmail = "owner@demo.local";
    private const string DefaultOwnerPassword = "Owner@123";
    private const string DefaultStaffEmail = "staff01@demo.local";
    private const string DefaultStaffPassword = "Staff@123";
    private const string DemoTenantName = "Demo Retail Chain";
    private const string DemoStoreCode = "DEMO-01";

    public static async Task SeedAsync(
        StoreFlowDbContext db,
        IConfiguration configuration,
        PasswordHasher<AspNetUser> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        await SeedRoleAsync(db, AppRoles.Admin, cancellationToken);
        await SeedRoleAsync(db, AppRoles.Owner, cancellationToken);
        await SeedRoleAsync(db, AppRoles.Staff, cancellationToken);

        var adminEmail = configuration["Seed:Admin:Email"] ?? DefaultAdminEmail;
        var adminPassword = configuration["Seed:Admin:Password"] ?? DefaultAdminPassword;
        var admin = await EnsureUserAsync(
            db,
            passwordHasher,
            adminEmail,
            adminPassword,
            "System Administrator",
            null,
            "Seed:Admin",
            cancellationToken);
        await EnsureUserRoleAsync(db, admin, AppRoles.Admin, cancellationToken);

        var ownerEmail = configuration["Seed:Owner:Email"] ?? DefaultOwnerEmail;
        var ownerPassword = configuration["Seed:Owner:Password"] ?? DefaultOwnerPassword;
        var owner = await EnsureUserAsync(
            db,
            passwordHasher,
            ownerEmail,
            ownerPassword,
            "Demo Owner",
            null,
            "Seed:Owner",
            cancellationToken);
        await EnsureUserRoleAsync(db, owner, AppRoles.Owner, cancellationToken);

        var tenant = await EnsureDemoTenantAsync(db, owner, cancellationToken);
        if (owner.TenantId != tenant.Id)
        {
            owner.TenantId = tenant.Id;
            owner.UpdatedAt = DateTime.UtcNow;
            owner.UpdatedBy = "seed";
            await db.SaveChangesAsync(cancellationToken);
        }

        var store = await EnsureDemoStoreAsync(db, tenant, owner, cancellationToken);

        var staffEmail = configuration["Seed:Staff:Email"] ?? DefaultStaffEmail;
        var staffPassword = configuration["Seed:Staff:Password"] ?? DefaultStaffPassword;
        var staff = await EnsureUserAsync(
            db,
            passwordHasher,
            staffEmail,
            staffPassword,
            "Demo Staff 01",
            tenant.Id,
            "Seed:Staff",
            cancellationToken);
        await EnsureUserRoleAsync(db, staff, AppRoles.Staff, cancellationToken);
        await EnsureStaffStoreAsync(db, tenant, store, staff, owner, cancellationToken);
    }

    private static async Task<AspNetUser> EnsureUserAsync(
        StoreFlowDbContext db,
        PasswordHasher<AspNetUser> passwordHasher,
        string email,
        string password,
        string fullName,
        Guid? tenantId,
        string createdBy,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = Normalize(email);
        var user = await db.AspNetUsers
            .Include(x => x.Roles)
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new AspNetUser
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = email,
                NormalizedUserName = normalizedEmail,
                Email = email,
                NormalizedEmail = normalizedEmail,
                EmailConfirmed = true,
                FullName = fullName,
                Status = UserStatuses.Active,
                TenantId = tenantId,
                LockoutEnabled = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);

            db.AspNetUsers.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var changed = false;

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                user.PasswordHash = passwordHasher.HashPassword(user, password);
                changed = true;
            }

            if (!string.Equals(user.Status, UserStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                user.Status = UserStatuses.Active;
                changed = true;
            }

            if (user.LockoutEnd.HasValue)
            {
                user.LockoutEnd = null;
                user.AccessFailedCount = 0;
                changed = true;
            }

            if (tenantId.HasValue && user.TenantId != tenantId)
            {
                user.TenantId = tenantId;
                changed = true;
            }

            user.SecurityStamp ??= Guid.NewGuid().ToString("N");
            user.ConcurrencyStamp ??= Guid.NewGuid().ToString("N");

            if (changed)
            {
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = "seed";
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return user;
    }

    private static async Task SeedRoleAsync(StoreFlowDbContext db, string roleName, CancellationToken cancellationToken)
    {
        var normalizedName = Normalize(roleName);
        var exists = await db.AspNetRoles.AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken);
        if (exists)
        {
            return;
        }

        db.AspNetRoles.Add(new AspNetRole
        {
            Id = roleName,
            Name = roleName,
            NormalizedName = normalizedName,
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureUserRoleAsync(
        StoreFlowDbContext db,
        AspNetUser user,
        string roleName,
        CancellationToken cancellationToken)
    {
        var role = await db.AspNetRoles.FirstAsync(x => x.Id == roleName, cancellationToken);
        if (user.Roles.Any(x => x.Id == roleName))
        {
            return;
        }

        user.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Tenant> EnsureDemoTenantAsync(
        StoreFlowDbContext db,
        AspNetUser owner,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(
            x => x.OwnerUserId == owner.Id || x.Name == DemoTenantName,
            cancellationToken);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = DemoTenantName,
                OwnerUserId = owner.Id,
                Email = owner.Email,
                Phone = "0900000001",
                Address = "Demo address",
                Status = TenantStatuses.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = owner.Id
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var changed = false;
            if (tenant.OwnerUserId != owner.Id)
            {
                tenant.OwnerUserId = owner.Id;
                changed = true;
            }

            if (!string.Equals(tenant.Status, TenantStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                tenant.Status = TenantStatuses.Active;
                changed = true;
            }

            if (tenant.IsDeleted)
            {
                tenant.IsDeleted = false;
                changed = true;
            }

            if (changed)
            {
                tenant.UpdatedAt = DateTime.UtcNow;
                tenant.UpdatedBy = "seed";
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return tenant;
    }

    private static async Task<Store> EnsureDemoStoreAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        AspNetUser owner,
        CancellationToken cancellationToken)
    {
        var store = await db.Stores.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.Code == DemoStoreCode,
            cancellationToken);

        if (store is null)
        {
            store = new Store
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Demo Store 01",
                Code = DemoStoreCode,
                Address = "Demo store address",
                Phone = "0900000002",
                Status = StoreStatuses.Active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = owner.Id
            };
            db.Stores.Add(store);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var changed = false;
            if (!string.Equals(store.Status, StoreStatuses.Active, StringComparison.OrdinalIgnoreCase))
            {
                store.Status = StoreStatuses.Active;
                changed = true;
            }

            if (store.IsDeleted)
            {
                store.IsDeleted = false;
                changed = true;
            }

            if (changed)
            {
                store.UpdatedAt = DateTime.UtcNow;
                store.UpdatedBy = "seed";
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return store;
    }

    private static async Task EnsureStaffStoreAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        Store store,
        AspNetUser staff,
        AspNetUser owner,
        CancellationToken cancellationToken)
    {
        var userStore = await db.UserStores.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.StoreId == store.Id && x.UserId == staff.Id,
            cancellationToken);

        if (userStore is null)
        {
            db.UserStores.Add(new UserStore
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                StoreId = store.Id,
                UserId = staff.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = owner.Id
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (!userStore.IsActive)
        {
            userStore.IsActive = true;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
