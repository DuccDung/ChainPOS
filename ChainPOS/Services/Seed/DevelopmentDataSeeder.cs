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
    private const string DemoTenantName = "TechZone Vietnam Retail";
    private const string DemoStoreCode = "TZ-HCM-01";
    private const string DemoPlanName = "Business Demo";

    public static async Task SeedAsync(
        StoreFlowDbContext db,
        IConfiguration configuration,
        PasswordHasher<AspNetUser> passwordHasher,
        CancellationToken cancellationToken = default)
    {
        await SeedRoleAsync(db, AppRoles.Admin, cancellationToken);
        await SeedRoleAsync(db, AppRoles.Owner, cancellationToken);
        await SeedRoleAsync(db, AppRoles.Staff, cancellationToken);

        var admin = await EnsureUserAsync(
            db,
            passwordHasher,
            configuration["Seed:Admin:Email"] ?? DefaultAdminEmail,
            configuration["Seed:Admin:Password"] ?? DefaultAdminPassword,
            "System Administrator",
            null,
            "Seed:Admin",
            cancellationToken);
        await EnsureUserRoleAsync(db, admin, AppRoles.Admin, cancellationToken);

        var demoOwner = await EnsureUserAsync(
            db,
            passwordHasher,
            configuration["Seed:Owner:Email"] ?? DefaultOwnerEmail,
            configuration["Seed:Owner:Password"] ?? DefaultOwnerPassword,
            "Tran Minh Quan",
            null,
            "Seed:Owner",
            cancellationToken,
            phoneNumber: "0901000001");
        await EnsureUserRoleAsync(db, demoOwner, AppRoles.Owner, cancellationToken);

        var plan = await EnsureSubscriptionPlanAsync(db, cancellationToken);
        var demoTenant = await EnsureTenantAsync(
            db,
            demoOwner,
            DemoTenantName,
            TenantStatuses.Active,
            "0312345678",
            "72 Le Thanh Ton, District 1, Ho Chi Minh City",
            "0901000001",
            "hello@techzone.local",
            cancellationToken);
        await EnsureOwnerTenantAsync(db, demoOwner, demoTenant, cancellationToken);
        var demoSubscription = await EnsureTenantSubscriptionAsync(db, demoTenant, plan, cancellationToken);
        await EnsureDemoSystemPaymentsAsync(db, demoTenant, demoSubscription, cancellationToken);

        var stores = await EnsureDemoStoresAsync(db, demoTenant, demoOwner, cancellationToken);
        var staff = await EnsureDemoStaffAsync(db, passwordHasher, demoTenant, demoOwner, stores, cancellationToken);
        var categories = await EnsureDemoCategoriesAsync(db, demoTenant, demoOwner, cancellationToken);
        var products = await EnsureDemoProductsAsync(db, demoTenant, demoOwner, categories, cancellationToken);
        var storeProducts = await EnsureDemoStoreProductsAsync(db, demoTenant, demoOwner, stores, products, cancellationToken);
        await EnsureDemoInventoryAsync(db, demoTenant, demoOwner, storeProducts, cancellationToken);
        await EnsureDemoSalesDataAsync(db, demoTenant, demoOwner, stores, staff, storeProducts, cancellationToken);

        await EnsureExtraOwnerTenantsAsync(db, passwordHasher, plan, admin, cancellationToken);
        await EnsureSeedAuditLogsAsync(db, admin, demoOwner, demoTenant, stores, staff, categories, products, storeProducts, cancellationToken);
        await EnsureAuditViewerDemoLogsAsync(db, admin, demoOwner, demoTenant, stores, staff, products, storeProducts, cancellationToken);
    }

    private static async Task<AspNetUser> EnsureUserAsync(
        StoreFlowDbContext db,
        PasswordHasher<AspNetUser> passwordHasher,
        string email,
        string password,
        string fullName,
        Guid? tenantId,
        string createdBy,
        CancellationToken cancellationToken,
        string? phoneNumber = null)
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
                PhoneNumber = phoneNumber,
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

            if (!string.IsNullOrWhiteSpace(phoneNumber) && user.PhoneNumber != phoneNumber)
            {
                user.PhoneNumber = phoneNumber;
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

    private static async Task<SubscriptionPlan> EnsureSubscriptionPlanAsync(
        StoreFlowDbContext db,
        CancellationToken cancellationToken)
    {
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(x => x.Name == DemoPlanName, cancellationToken);
        if (plan is null)
        {
            plan = new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                Name = DemoPlanName,
                BillingCycle = "Monthly",
                Price = 1990000m,
                MaxStores = 10,
                MaxStaff = 50,
                MaxProducts = 200,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            };
            db.SubscriptionPlans.Add(plan);
        }
        else
        {
            plan.BillingCycle = "Monthly";
            plan.Price = 1990000m;
            plan.MaxStores = 10;
            plan.MaxStaff = 50;
            plan.MaxProducts = 200;
            plan.IsActive = true;
            plan.IsDeleted = false;
            plan.UpdatedAt = DateTime.UtcNow;
            plan.UpdatedBy = "seed";
        }

        await db.SaveChangesAsync(cancellationToken);
        return plan;
    }

    private static async Task<Tenant> EnsureTenantAsync(
        StoreFlowDbContext db,
        AspNetUser owner,
        string name,
        string status,
        string taxCode,
        string address,
        string phone,
        string email,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(
            x => x.OwnerUserId == owner.Id || x.Name == name || x.Name == "Demo Retail Chain",
            cancellationToken);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = name,
                OwnerUserId = owner.Id,
                TaxCode = taxCode,
                Email = email,
                Phone = phone,
                Address = address,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = owner.Id
            };
            db.Tenants.Add(tenant);
        }
        else
        {
            tenant.Name = name;
            tenant.OwnerUserId = owner.Id;
            tenant.TaxCode = taxCode;
            tenant.Email = email;
            tenant.Phone = phone;
            tenant.Address = address;
            tenant.Status = status;
            tenant.IsDeleted = false;
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = "seed";
        }

        await db.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    private static async Task EnsureOwnerTenantAsync(
        StoreFlowDbContext db,
        AspNetUser owner,
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        if (owner.TenantId == tenant.Id)
        {
            return;
        }

        owner.TenantId = tenant.Id;
        owner.UpdatedAt = DateTime.UtcNow;
        owner.UpdatedBy = "seed";
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<TenantSubscription> EnsureTenantSubscriptionAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        SubscriptionPlan plan,
        CancellationToken cancellationToken)
    {
        var subscription = await db.TenantSubscriptions.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.PlanId == plan.Id && x.Status == "Active",
            cancellationToken);

        if (subscription is null)
        {
            subscription = new TenantSubscription
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                PlanId = plan.Id,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
                Status = "Active",
                AutoRenew = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            };
            db.TenantSubscriptions.Add(subscription);
        }
        else
        {
            subscription.EndDate = new DateOnly(2026, 12, 31);
            subscription.AutoRenew = true;
            subscription.UpdatedAt = DateTime.UtcNow;
            subscription.UpdatedBy = "seed";
        }

        await db.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    private static async Task EnsureDemoSystemPaymentsAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        TenantSubscription subscription,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new SystemPaymentSeed(1990000m, PaymentMethods.BankTransfer, PaymentStatuses.Paid, new DateTime(2026, 3, 1, 4, 30, 0, DateTimeKind.Utc), "/invoices/system/techzone-2026-03.pdf", new DateTime(2026, 3, 1, 4, 0, 0, DateTimeKind.Utc)),
            new SystemPaymentSeed(1990000m, PaymentMethods.Card, PaymentStatuses.Paid, new DateTime(2026, 4, 1, 4, 45, 0, DateTimeKind.Utc), "/invoices/system/techzone-2026-04.pdf", new DateTime(2026, 4, 1, 4, 0, 0, DateTimeKind.Utc)),
            new SystemPaymentSeed(1990000m, PaymentMethods.BankTransfer, PaymentStatuses.Pending, null, "/invoices/system/techzone-2026-05.pdf", DateTime.UtcNow.AddDays(-2)),
            new SystemPaymentSeed(1990000m, PaymentMethods.Momo, PaymentStatuses.Failed, null, "/invoices/system/techzone-2026-05-retry.pdf", DateTime.UtcNow.AddDays(-1))
        };

        foreach (var seed in seeds)
        {
            var exists = await db.SystemPayments.AnyAsync(
                x => x.TenantId == tenant.Id
                    && x.SubscriptionId == subscription.Id
                    && x.InvoiceUrl == seed.InvoiceUrl,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            db.SystemPayments.Add(new SystemPayment
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                SubscriptionId = subscription.Id,
                Amount = seed.Amount,
                Method = seed.Method,
                Status = seed.Status,
                PaidAt = seed.PaidAt,
                InvoiceUrl = seed.InvoiceUrl,
                CreatedAt = seed.CreatedAt
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, Store>> EnsureDemoStoresAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        AspNetUser owner,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new StoreSeed(DemoStoreCode, "TechZone Vincom Dong Khoi", "72 Le Thanh Ton, District 1, Ho Chi Minh City", "02838220001", StoreStatuses.Active),
            new StoreSeed("TZ-HCM-02", "TechZone Crescent Mall", "101 Ton Dat Tien, District 7, Ho Chi Minh City", "02854130002", StoreStatuses.Active),
            new StoreSeed("TZ-HN-01", "TechZone Hoan Kiem", "24 Trang Tien, Hoan Kiem, Ha Noi", "02439360003", StoreStatuses.Active),
            new StoreSeed("TZ-DN-01", "TechZone Bach Dang", "155 Bach Dang, Hai Chau, Da Nang", "02363660004", StoreStatuses.Inactive)
        };

        var stores = new Dictionary<string, Store>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in seeds)
        {
            var store = await db.Stores.FirstOrDefaultAsync(
                x => x.TenantId == tenant.Id && x.Code == seed.Code,
                cancellationToken);

            if (store is null)
            {
                store = new Store
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = seed.Name,
                    Code = seed.Code,
                    Address = seed.Address,
                    Phone = seed.Phone,
                    Status = seed.Status,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = owner.Id
                };
                db.Stores.Add(store);
            }
            else
            {
                store.Name = seed.Name;
                store.Address = seed.Address;
                store.Phone = seed.Phone;
                store.Status = seed.Status;
                store.IsDeleted = false;
                store.UpdatedAt = DateTime.UtcNow;
                store.UpdatedBy = "seed";
            }

            stores[seed.Code] = store;
        }

        await db.SaveChangesAsync(cancellationToken);
        return stores;
    }

    private static async Task<Dictionary<string, AspNetUser>> EnsureDemoStaffAsync(
        StoreFlowDbContext db,
        PasswordHasher<AspNetUser> passwordHasher,
        Tenant tenant,
        AspNetUser owner,
        IReadOnlyDictionary<string, Store> stores,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new StaffSeed(DefaultStaffEmail, DefaultStaffPassword, "Nguyen Minh Anh", "0902000001", new[] { "TZ-HCM-01", "TZ-HCM-02" }),
            new StaffSeed("staff02@demo.local", "Staff@123", "Le Thu Ha", "0902000002", new[] { "TZ-HCM-01" }),
            new StaffSeed("staff03@demo.local", "Staff@123", "Pham Gia Huy", "0902000003", new[] { "TZ-HN-01" }),
            new StaffSeed("staff04@demo.local", "Staff@123", "Vo Quang Kien", "0902000004", new[] { "TZ-DN-01" })
        };

        var staff = new Dictionary<string, AspNetUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in seeds)
        {
            var user = await EnsureUserAsync(
                db,
                passwordHasher,
                seed.Email,
                seed.Password,
                seed.FullName,
                tenant.Id,
                owner.Id,
                cancellationToken,
                seed.Phone);
            await EnsureUserRoleAsync(db, user, AppRoles.Staff, cancellationToken);

            foreach (var storeCode in seed.StoreCodes)
            {
                if (stores.TryGetValue(storeCode, out var store))
                {
                    await EnsureStaffStoreAsync(db, tenant, store, user, owner, cancellationToken);
                }
            }

            staff[seed.Email] = user;
        }

        return staff;
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
        }
        else
        {
            userStore.IsActive = true;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, Category>> EnsureDemoCategoriesAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        AspNetUser owner,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new CategorySeed("Laptops", "MacBook, ultrabook and Windows laptop models."),
            new CategorySeed("Smartphones", "Flagship and mid-range smartphones."),
            new CategorySeed("Audio", "Headphones, earbuds and speaker products."),
            new CategorySeed("Accessories", "Keyboards, mice, chargers, hubs and power banks."),
            new CategorySeed("Monitors", "Office and creator displays."),
            new CategorySeed("Storage", "SSD, memory cards and portable storage.")
        };

        var categories = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in seeds)
        {
            var category = await db.Categories.FirstOrDefaultAsync(
                x => x.TenantId == tenant.Id && x.Name == seed.Name && !x.IsDeleted,
                cancellationToken);

            if (category is null)
            {
                category = new Category
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = seed.Name,
                    Description = seed.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = owner.Id
                };
                db.Categories.Add(category);
            }
            else
            {
                category.Description = seed.Description;
                category.IsActive = true;
                category.UpdatedAt = DateTime.UtcNow;
                category.UpdatedBy = "seed";
            }

            categories[seed.Name] = category;
        }

        await db.SaveChangesAsync(cancellationToken);
        return categories;
    }

    private static async Task<Dictionary<string, Product>> EnsureDemoProductsAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        AspNetUser owner,
        IReadOnlyDictionary<string, Category> categories,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new ProductSeed("Laptops", "Apple MacBook Pro 14-inch M3 Pro 18GB/512GB", "MBP14-M3P-18-512", "100000000001", "14.2-inch Liquid Retina XDR display, Apple M3 Pro chip, 18GB unified memory, 512GB SSD.", 52990000m, 48000000m, true),
            new ProductSeed("Laptops", "Apple MacBook Air 13-inch M3 8GB/256GB", "MBA13-M3-8-256", "100000000002", "13.6-inch Liquid Retina display, Apple M3 chip, 8GB unified memory, 256GB SSD.", 27990000m, 24700000m, true),
            new ProductSeed("Laptops", "Dell XPS 13 Plus 9320 i7 16GB/512GB", "DELL-XPS13P-I7-512", "100000000003", "13.4-inch InfinityEdge display, Intel Core i7, 16GB RAM, 512GB SSD.", 39990000m, 35200000m, true),
            new ProductSeed("Smartphones", "Apple iPhone 15 Pro 256GB Natural Titanium", "IPH15P-256-NT", "100000000004", "6.1-inch Super Retina XDR display, A17 Pro chip, 256GB storage.", 27990000m, 24900000m, true),
            new ProductSeed("Smartphones", "Samsung Galaxy S24 Ultra 256GB Titanium Gray", "SS-S24U-256-GRY", "100000000005", "6.8-inch Dynamic AMOLED 2X display, Snapdragon 8 Gen 3, S Pen, 256GB storage.", 29990000m, 26300000m, true),
            new ProductSeed("Audio", "Apple AirPods Pro 2nd Generation USB-C", "APP2-USBC", "100000000006", "Active Noise Cancellation, Adaptive Audio, USB-C MagSafe charging case.", 6190000m, 5100000m, true),
            new ProductSeed("Audio", "Sony WH-1000XM5 Wireless Noise Cancelling Headphones", "SONY-WH1000XM5-BLK", "100000000007", "Over-ear wireless headphones with industry noise cancellation and 30-hour battery life.", 8490000m, 7200000m, true),
            new ProductSeed("Accessories", "Logitech MX Master 3S Wireless Mouse Graphite", "LOGI-MX3S-GR", "100000000008", "Ergonomic wireless mouse with 8K DPI sensor and quiet clicks.", 2490000m, 1900000m, true),
            new ProductSeed("Accessories", "Logitech MX Keys S Wireless Keyboard Graphite", "LOGI-MXKEYSS-GR", "100000000009", "Low-profile wireless keyboard with smart illumination and multi-device support.", 2690000m, 2100000m, true),
            new ProductSeed("Monitors", "Dell UltraSharp U2723QE 27-inch 4K USB-C Monitor", "DELL-U2723QE", "100000000010", "27-inch 4K IPS Black monitor with USB-C hub and 90W power delivery.", 13990000m, 11900000m, true),
            new ProductSeed("Storage", "Samsung 990 PRO 1TB NVMe PCIe 4.0 SSD", "SS-990PRO-1TB", "100000000011", "PCIe 4.0 NVMe M.2 SSD, up to 7450 MB/s sequential read speed.", 3290000m, 2600000m, true),
            new ProductSeed("Accessories", "Anker 737 Power Bank 24000mAh 140W", "ANKER-737-24K", "100000000012", "24000mAh portable charger with 140W USB-C output and smart digital display.", 3990000m, 3150000m, true),
            new ProductSeed("Accessories", "Apple USB-C Digital AV Multiport Adapter", "APL-USBC-AV-ADPT", "100000000013", "USB-C adapter with HDMI, USB-A and USB-C charging pass-through.", 1890000m, 1420000m, false)
        };

        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in seeds)
        {
            var category = categories[seed.CategoryName];
            var product = await db.Products.FirstOrDefaultAsync(
                x => x.TenantId == tenant.Id && x.Sku == seed.Sku,
                cancellationToken);

            if (product is null)
            {
                product = new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    CategoryId = category.Id,
                    Name = seed.Name,
                    Sku = seed.Sku,
                    Barcode = seed.Barcode,
                    Description = seed.Description,
                    Price = seed.Price,
                    CostPrice = seed.CostPrice,
                    IsActive = seed.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = owner.Id
                };
                db.Products.Add(product);
            }
            else
            {
                product.CategoryId = category.Id;
                product.Name = seed.Name;
                product.Barcode = seed.Barcode;
                product.Description = seed.Description;
                product.Price = seed.Price;
                product.CostPrice = seed.CostPrice;
                product.IsActive = seed.IsActive;
                product.IsDeleted = false;
                product.UpdatedAt = DateTime.UtcNow;
                product.UpdatedBy = "seed";
            }

            products[seed.Sku] = product;
        }

        await db.SaveChangesAsync(cancellationToken);
        return products;
    }

    private static async Task<Dictionary<string, StoreProduct>> EnsureDemoStoreProductsAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        AspNetUser owner,
        IReadOnlyDictionary<string, Store> stores,
        IReadOnlyDictionary<string, Product> products,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new StoreProductSeed("TZ-HCM-01", "MBP14-M3P-18-512", 52490000m, true),
            new StoreProductSeed("TZ-HCM-01", "IPH15P-256-NT", 27490000m, true),
            new StoreProductSeed("TZ-HCM-01", "SS-S24U-256-GRY", 29490000m, true),
            new StoreProductSeed("TZ-HCM-01", "LOGI-MX3S-GR", null, true),
            new StoreProductSeed("TZ-HCM-01", "DELL-U2723QE", 13690000m, true),
            new StoreProductSeed("TZ-HCM-02", "MBA13-M3-8-256", 27490000m, true),
            new StoreProductSeed("TZ-HCM-02", "APP2-USBC", 5990000m, true),
            new StoreProductSeed("TZ-HCM-02", "SONY-WH1000XM5-BLK", 8290000m, true),
            new StoreProductSeed("TZ-HCM-02", "ANKER-737-24K", null, true),
            new StoreProductSeed("TZ-HN-01", "DELL-XPS13P-I7-512", 38990000m, true),
            new StoreProductSeed("TZ-HN-01", "LOGI-MXKEYSS-GR", null, true),
            new StoreProductSeed("TZ-HN-01", "SS-990PRO-1TB", 3190000m, true),
            new StoreProductSeed("TZ-DN-01", "APL-USBC-AV-ADPT", null, false)
        };

        var storeProducts = new Dictionary<string, StoreProduct>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in seeds)
        {
            if (!stores.TryGetValue(seed.StoreCode, out var store) ||
                !products.TryGetValue(seed.Sku, out var product))
            {
                continue;
            }

            var storeProduct = await db.StoreProducts.FirstOrDefaultAsync(
                x => x.TenantId == tenant.Id && x.StoreId == store.Id && x.ProductId == product.Id,
                cancellationToken);

            if (storeProduct is null)
            {
                storeProduct = new StoreProduct
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    StoreId = store.Id,
                    ProductId = product.Id,
                    SellingPrice = seed.SellingPrice,
                    IsAvailable = seed.IsAvailable,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = owner.Id
                };
                db.StoreProducts.Add(storeProduct);
            }
            else
            {
                storeProduct.SellingPrice = seed.SellingPrice;
                storeProduct.IsAvailable = seed.IsAvailable;
                storeProduct.UpdatedAt = DateTime.UtcNow;
                storeProduct.UpdatedBy = "seed";
            }

            storeProducts[$"{seed.StoreCode}:{seed.Sku}"] = storeProduct;
        }

        await db.SaveChangesAsync(cancellationToken);
        return storeProducts;
    }

    private static async Task EnsureDemoInventoryAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        AspNetUser owner,
        IReadOnlyDictionary<string, StoreProduct> storeProducts,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new InventorySeed("TZ-HCM-01:MBP14-M3P-18-512", 12m, 3m),
            new InventorySeed("TZ-HCM-01:IPH15P-256-NT", 18m, 5m),
            new InventorySeed("TZ-HCM-01:SS-S24U-256-GRY", 7m, 5m),
            new InventorySeed("TZ-HCM-01:LOGI-MX3S-GR", 4m, 8m),
            new InventorySeed("TZ-HCM-01:DELL-U2723QE", 6m, 2m),
            new InventorySeed("TZ-HCM-02:MBA13-M3-8-256", 10m, 3m),
            new InventorySeed("TZ-HCM-02:APP2-USBC", 3m, 10m),
            new InventorySeed("TZ-HCM-02:SONY-WH1000XM5-BLK", 0m, 4m),
            new InventorySeed("TZ-HCM-02:ANKER-737-24K", 15m, 6m),
            new InventorySeed("TZ-HN-01:DELL-XPS13P-I7-512", 5m, 2m),
            new InventorySeed("TZ-HN-01:LOGI-MXKEYSS-GR", 9m, 5m),
            new InventorySeed("TZ-HN-01:SS-990PRO-1TB", 2m, 6m)
        };

        foreach (var seed in seeds)
        {
            if (!storeProducts.TryGetValue(seed.StoreProductKey, out var storeProduct))
            {
                continue;
            }

            var inventory = await db.Inventories.FirstOrDefaultAsync(
                x => x.TenantId == tenant.Id
                    && x.StoreId == storeProduct.StoreId
                    && x.ProductId == storeProduct.ProductId,
                cancellationToken);
            if (inventory is null)
            {
                inventory = new ChainPOS.Models.Inventory
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    StoreId = storeProduct.StoreId,
                    ProductId = storeProduct.ProductId,
                    Quantity = seed.Quantity,
                    MinQuantity = seed.MinQuantity,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = owner.Id
                };
                db.Inventories.Add(inventory);

                if (seed.Quantity > 0)
                {
                    db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        StoreId = storeProduct.StoreId,
                        ProductId = storeProduct.ProductId,
                        Type = InventoryTransactionTypes.Import,
                        Quantity = seed.Quantity,
                        BeforeQuantity = 0m,
                        AfterQuantity = seed.Quantity,
                        Reason = "Seed opening stock",
                        ReferenceType = "Seed",
                        CreatedBy = owner.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            else if (inventory.MinQuantity != seed.MinQuantity)
            {
                inventory.MinQuantity = seed.MinQuantity;
                inventory.UpdatedAt = DateTime.UtcNow;
                inventory.UpdatedBy = "seed";
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureDemoSalesDataAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        AspNetUser owner,
        IReadOnlyDictionary<string, Store> stores,
        IReadOnlyDictionary<string, AspNetUser> staff,
        IReadOnlyDictionary<string, StoreProduct> storeProducts,
        CancellationToken cancellationToken)
    {
        if (!stores.TryGetValue("TZ-HCM-01", out var hcm01)
            || !stores.TryGetValue("TZ-HCM-02", out var hcm02)
            || !staff.TryGetValue(DefaultStaffEmail, out var staff01)
            || !staff.TryGetValue("staff02@demo.local", out var staff02))
        {
            return;
        }

        var businessDay = new DateTime(2026, 5, 29, 0, 0, 0, DateTimeKind.Utc);
        var yesterday = businessDay.AddDays(-1);

        var hcm01Yesterday = await EnsureDemoShiftAsync(
            db,
            tenant,
            hcm01,
            staff01,
            yesterday.AddHours(1),
            yesterday.AddHours(9),
            5_000_000m,
            34_540_000m,
            cancellationToken);

        var hcm01Today = await EnsureDemoShiftAsync(
            db,
            tenant,
            hcm01,
            staff02,
            businessDay.AddHours(1),
            businessDay.AddHours(7).AddMinutes(30),
            3_000_000m,
            3_000_000m,
            cancellationToken);

        var hcm02Yesterday = await EnsureDemoShiftAsync(
            db,
            tenant,
            hcm02,
            staff01,
            yesterday.AddHours(10),
            yesterday.AddHours(15),
            2_000_000m,
            2_000_000m,
            cancellationToken);

        var hcm02Open = await EnsureDemoOpenShiftAsync(
            db,
            tenant,
            hcm02,
            staff01,
            businessDay.AddHours(2),
            4_000_000m,
            cancellationToken);

        var orders = new List<(Order Order, bool IsCancelled)>();

        var order = await EnsureDemoOrderAsync(
            db,
            tenant,
            owner,
            hcm01,
            staff01,
            hcm01Yesterday,
            storeProducts,
            "POS-DEMO-0001",
            yesterday.AddHours(2).AddMinutes(15),
            PaymentMethods.Cash,
            "CASH-DEMO-0001",
            490_000m,
            false,
            "Walk-in customer bought phone accessories.",
            new[]
            {
                new OrderItemSeed("IPH15P-256-NT", 1m),
                new OrderItemSeed("LOGI-MX3S-GR", 1m)
            },
            cancellationToken);
        if (order is not null)
        {
            orders.Add((order, false));
        }

        order = await EnsureDemoOrderAsync(
            db,
            tenant,
            owner,
            hcm01,
            staff01,
            hcm01Yesterday,
            storeProducts,
            "POS-DEMO-0002",
            yesterday.AddHours(4).AddMinutes(40),
            PaymentMethods.Card,
            "CARD-DEMO-0002",
            0m,
            false,
            "MacBook checkout paid by card.",
            new[]
            {
                new OrderItemSeed("MBP14-M3P-18-512", 1m)
            },
            cancellationToken);
        if (order is not null)
        {
            orders.Add((order, false));
        }

        order = await EnsureDemoOrderAsync(
            db,
            tenant,
            owner,
            hcm01,
            staff02,
            hcm01Today,
            storeProducts,
            "POS-DEMO-0003",
            businessDay.AddHours(3).AddMinutes(20),
            PaymentMethods.BankTransfer,
            "BANK-DEMO-0003",
            0m,
            false,
            "Bundle checkout for monitor and flagship phone.",
            new[]
            {
                new OrderItemSeed("SS-S24U-256-GRY", 1m),
                new OrderItemSeed("DELL-U2723QE", 1m)
            },
            cancellationToken);
        if (order is not null)
        {
            orders.Add((order, false));
        }

        order = await EnsureDemoOrderAsync(
            db,
            tenant,
            owner,
            hcm02,
            staff01,
            hcm02Yesterday,
            storeProducts,
            "POS-DEMO-0004",
            yesterday.AddHours(11).AddMinutes(25),
            PaymentMethods.Momo,
            "MOMO-DEMO-0004",
            0m,
            false,
            "Audio and power accessory checkout.",
            new[]
            {
                new OrderItemSeed("APP2-USBC", 1m),
                new OrderItemSeed("ANKER-737-24K", 2m)
            },
            cancellationToken);
        if (order is not null)
        {
            orders.Add((order, false));
        }

        order = await EnsureDemoOrderAsync(
            db,
            tenant,
            owner,
            hcm02,
            staff01,
            hcm02Yesterday,
            storeProducts,
            "POS-DEMO-VOID-0001",
            yesterday.AddHours(13).AddMinutes(5),
            PaymentMethods.Cash,
            "CASH-DEMO-VOID-0001",
            0m,
            true,
            "Cancelled demo order to show void and stock return flow.",
            new[]
            {
                new OrderItemSeed("MBA13-M3-8-256", 1m)
            },
            cancellationToken);
        if (order is not null)
        {
            orders.Add((order, true));
        }

        if (hcm02Open is not null && hcm02Open.StoreId == hcm02.Id && hcm02Open.OpenedBy == staff01.Id)
        {
            order = await EnsureDemoOrderAsync(
                db,
                tenant,
                owner,
                hcm02,
                staff01,
                hcm02Open,
                storeProducts,
                "POS-DEMO-OPEN-0001",
                businessDay.AddHours(3).AddMinutes(45),
                PaymentMethods.Cash,
                "CASH-DEMO-OPEN-0001",
                0m,
                false,
                "Live open-shift checkout for staff demo account.",
                new[]
                {
                    new OrderItemSeed("ANKER-737-24K", 1m)
                },
                cancellationToken);
            if (order is not null)
            {
                orders.Add((order, false));
            }
        }

        await UpdateShiftCashSummaryAsync(db, hcm01Yesterday, cancellationToken);
        await UpdateShiftCashSummaryAsync(db, hcm01Today, cancellationToken);
        await UpdateShiftCashSummaryAsync(db, hcm02Yesterday, cancellationToken);

        await EnsureShiftAuditLogsAsync(db, hcm01Yesterday, staff01.Id, cancellationToken);
        await EnsureShiftAuditLogsAsync(db, hcm01Today, staff02.Id, cancellationToken);
        await EnsureShiftAuditLogsAsync(db, hcm02Yesterday, staff01.Id, cancellationToken);
        if (hcm02Open is not null && hcm02Open.OpenedBy == staff01.Id)
        {
            await EnsureShiftAuditLogsAsync(db, hcm02Open, staff01.Id, cancellationToken);
        }

        foreach (var (demoOrder, isCancelled) in orders)
        {
            await EnsureAuditLogAsync(
                db,
                "CreateOrder",
                nameof(Order),
                demoOrder.Id.ToString(),
                demoOrder.CreatedBy ?? owner.Id,
                demoOrder.TenantId,
                demoOrder.StoreId,
                $"OrderCode={demoOrder.OrderCode}; Total={demoOrder.TotalAmount:#,##0.##}; Status={demoOrder.OrderStatus}",
                cancellationToken);

            if (isCancelled)
            {
                await EnsureAuditLogAsync(
                    db,
                    "CancelOrder",
                    nameof(Order),
                    demoOrder.Id.ToString(),
                    demoOrder.CancelledBy ?? demoOrder.CreatedBy ?? owner.Id,
                    demoOrder.TenantId,
                    demoOrder.StoreId,
                    $"OrderCode={demoOrder.OrderCode}; PaymentStatus={demoOrder.PaymentStatus}",
                    cancellationToken);
            }
        }
    }

    private static async Task<Shift> EnsureDemoShiftAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        Store store,
        AspNetUser openedBy,
        DateTime openedAt,
        DateTime? closedAt,
        decimal openingCash,
        decimal? closingCash,
        CancellationToken cancellationToken)
    {
        var status = closedAt.HasValue ? ShiftStatuses.Closed : ShiftStatuses.Open;
        var shift = await db.Shifts.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id
                && x.StoreId == store.Id
                && x.OpenedBy == openedBy.Id
                && x.OpenedAt == openedAt,
            cancellationToken);

        if (shift is null)
        {
            shift = new Shift
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                StoreId = store.Id,
                OpenedBy = openedBy.Id,
                OpenedAt = openedAt,
                ClosedBy = closedAt.HasValue ? openedBy.Id : null,
                ClosedAt = closedAt,
                OpeningCash = openingCash,
                ClosingCash = closingCash,
                Status = status
            };
            db.Shifts.Add(shift);
        }
        else
        {
            shift.OpeningCash = openingCash;
            shift.ClosedBy = closedAt.HasValue ? openedBy.Id : null;
            shift.ClosedAt = closedAt;
            shift.ClosingCash = closingCash;
            shift.Status = status;
        }

        await db.SaveChangesAsync(cancellationToken);
        return shift;
    }

    private static async Task<Shift?> EnsureDemoOpenShiftAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        Store store,
        AspNetUser openedBy,
        DateTime openedAt,
        decimal openingCash,
        CancellationToken cancellationToken)
    {
        var existingOpenShift = await db.Shifts.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id
                && x.OpenedBy == openedBy.Id
                && x.Status == ShiftStatuses.Open,
            cancellationToken);
        if (existingOpenShift is not null)
        {
            return existingOpenShift;
        }

        return await EnsureDemoShiftAsync(
            db,
            tenant,
            store,
            openedBy,
            openedAt,
            null,
            openingCash,
            null,
            cancellationToken);
    }

    private static async Task<Order?> EnsureDemoOrderAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        AspNetUser owner,
        Store store,
        AspNetUser staffUser,
        Shift shift,
        IReadOnlyDictionary<string, StoreProduct> storeProducts,
        string orderCode,
        DateTime createdAt,
        string paymentMethod,
        string transactionCode,
        decimal discountAmount,
        bool isCancelled,
        string note,
        IReadOnlyList<OrderItemSeed> items,
        CancellationToken cancellationToken)
    {
        if (shift.StoreId != store.Id)
        {
            return null;
        }

        var existingOrder = await db.Orders.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.OrderCode == orderCode,
            cancellationToken);
        if (existingOrder is not null)
        {
            return existingOrder;
        }

        var resolvedItems = new List<ResolvedOrderItemSeed>();
        foreach (var item in items)
        {
            if (!storeProducts.TryGetValue($"{store.Code}:{item.Sku}", out var storeProduct))
            {
                return null;
            }

            var product = await db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.Id == storeProduct.ProductId, cancellationToken);
            if (product is null)
            {
                return null;
            }

            var unitPrice = storeProduct.SellingPrice ?? product.Price;
            resolvedItems.Add(new ResolvedOrderItemSeed(
                product.Id,
                product.Name,
                product.Sku,
                item.Quantity,
                unitPrice,
                unitPrice * item.Quantity));
        }

        var subTotal = resolvedItems.Sum(x => x.LineTotal);
        if (discountAmount < 0m || discountAmount > subTotal)
        {
            return null;
        }

        var totalAmount = subTotal - discountAmount;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            StoreId = store.Id,
            OrderCode = orderCode,
            StaffUserId = staffUser.Id,
            ShiftId = shift.Id,
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            TaxAmount = 0m,
            TotalAmount = totalAmount,
            PaymentStatus = isCancelled ? OrderPaymentStatuses.Cancelled : OrderPaymentStatuses.Paid,
            OrderStatus = isCancelled ? OrderStatuses.Cancelled : OrderStatuses.Completed,
            Note = note,
            CreatedAt = createdAt,
            CreatedBy = staffUser.Id,
            UpdatedAt = isCancelled ? createdAt.AddMinutes(20) : null,
            UpdatedBy = isCancelled ? staffUser.Id : null,
            CancelledAt = isCancelled ? createdAt.AddMinutes(20) : null,
            CancelledBy = isCancelled ? staffUser.Id : null
        };
        db.Orders.Add(order);

        foreach (var item in resolvedItems)
        {
            db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                OrderId = order.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Sku = item.Sku,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = 0m,
                LineTotal = item.LineTotal
            });

            var inventory = await EnsureInventoryCanSellAsync(
                db,
                tenant,
                owner,
                store,
                item.ProductId,
                item.Quantity,
                createdAt,
                cancellationToken);

            var beforeSale = inventory.Quantity;
            inventory.Quantity -= item.Quantity;
            inventory.UpdatedAt = createdAt;
            inventory.UpdatedBy = staffUser.Id;
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                StoreId = store.Id,
                ProductId = item.ProductId,
                Type = InventoryTransactionTypes.Sale,
                Quantity = item.Quantity,
                BeforeQuantity = beforeSale,
                AfterQuantity = inventory.Quantity,
                Reason = $"Seed POS sale {orderCode}",
                ReferenceType = nameof(Order),
                ReferenceId = order.Id.ToString(),
                CreatedBy = staffUser.Id,
                CreatedAt = createdAt
            });

            if (isCancelled)
            {
                var beforeReturn = inventory.Quantity;
                inventory.Quantity += item.Quantity;
                inventory.UpdatedAt = createdAt.AddMinutes(20);
                inventory.UpdatedBy = staffUser.Id;
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    StoreId = store.Id,
                    ProductId = item.ProductId,
                    Type = InventoryTransactionTypes.Return,
                    Quantity = item.Quantity,
                    BeforeQuantity = beforeReturn,
                    AfterQuantity = inventory.Quantity,
                    Reason = $"Seed cancel order {orderCode}",
                    ReferenceType = nameof(Order),
                    ReferenceId = order.Id.ToString(),
                    CreatedBy = staffUser.Id,
                    CreatedAt = createdAt.AddMinutes(20)
                });
            }
        }

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            OrderId = order.Id,
            Method = paymentMethod,
            Amount = totalAmount,
            TransactionCode = transactionCode,
            PaidAt = createdAt,
            Status = isCancelled ? PaymentStatuses.Cancelled : PaymentStatuses.Paid,
            CreatedAt = createdAt
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return order;
    }

    private static async Task<ChainPOS.Models.Inventory> EnsureInventoryCanSellAsync(
        StoreFlowDbContext db,
        Tenant tenant,
        AspNetUser owner,
        Store store,
        Guid productId,
        decimal quantity,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        var inventory = await db.Inventories.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.StoreId == store.Id && x.ProductId == productId,
            cancellationToken);
        if (inventory is null)
        {
            inventory = new ChainPOS.Models.Inventory
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                StoreId = store.Id,
                ProductId = productId,
                Quantity = 0m,
                MinQuantity = 0m,
                UpdatedAt = createdAt.AddMinutes(-5),
                UpdatedBy = owner.Id
            };
            db.Inventories.Add(inventory);
        }

        if (inventory.Quantity >= quantity)
        {
            return inventory;
        }

        var before = inventory.Quantity;
        var topUpQuantity = quantity - inventory.Quantity + 5m;
        inventory.Quantity += topUpQuantity;
        inventory.UpdatedAt = createdAt.AddMinutes(-5);
        inventory.UpdatedBy = owner.Id;
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            StoreId = store.Id,
            ProductId = productId,
            Type = InventoryTransactionTypes.Import,
            Quantity = topUpQuantity,
            BeforeQuantity = before,
            AfterQuantity = inventory.Quantity,
            Reason = "Seed POS stock top-up",
            ReferenceType = "Seed",
            ReferenceId = $"Phase7:{store.Code}",
            CreatedBy = owner.Id,
            CreatedAt = createdAt.AddMinutes(-5)
        });

        return inventory;
    }

    private static async Task UpdateShiftCashSummaryAsync(
        StoreFlowDbContext db,
        Shift shift,
        CancellationToken cancellationToken)
    {
        if (shift.Status != ShiftStatuses.Closed)
        {
            return;
        }

        var cashSales = await db.Payments
            .Where(x => x.Method == PaymentMethods.Cash
                && x.Status == PaymentStatuses.Paid
                && x.Order.ShiftId == shift.Id
                && x.Order.OrderStatus != OrderStatuses.Cancelled)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var expectedCash = shift.OpeningCash + cashSales;
        shift.ExpectedCash = expectedCash;
        shift.ClosingCash ??= expectedCash;
        shift.DifferenceAmount = shift.ClosingCash - expectedCash;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureShiftAuditLogsAsync(
        StoreFlowDbContext db,
        Shift shift,
        string userId,
        CancellationToken cancellationToken)
    {
        await EnsureAuditLogAsync(
            db,
            "OpenShift",
            nameof(Shift),
            shift.Id.ToString(),
            shift.OpenedBy,
            shift.TenantId,
            shift.StoreId,
            $"OpeningCash={shift.OpeningCash:#,##0.##}; Status={shift.Status}",
            cancellationToken);

        if (shift.Status == ShiftStatuses.Closed)
        {
            await EnsureAuditLogAsync(
                db,
                "CloseShift",
                nameof(Shift),
                shift.Id.ToString(),
                shift.ClosedBy ?? userId,
                shift.TenantId,
                shift.StoreId,
                $"ClosingCash={shift.ClosingCash:#,##0.##}; ExpectedCash={shift.ExpectedCash:#,##0.##}; Difference={shift.DifferenceAmount:#,##0.##}",
                cancellationToken);
        }
    }

    private static async Task EnsureExtraOwnerTenantsAsync(
        StoreFlowDbContext db,
        PasswordHasher<AspNetUser> passwordHasher,
        SubscriptionPlan plan,
        AspNetUser admin,
        CancellationToken cancellationToken)
    {
        var seeds = new[]
        {
            new OwnerTenantSeed("owner.saigon@chainpos.local", "Owner@123", "Nguyen Hoang Nam", "Saigon Gadget Hub", TenantStatuses.Active, "0311111111", "45 Nguyen Hue, District 1, Ho Chi Minh City", "0903000001", "contact@saigongadget.local"),
            new OwnerTenantSeed("owner.hanoi@chainpos.local", "Owner@123", "Do Lan Phuong", "Ha Noi Digital Mart", TenantStatuses.Trial, "0102222222", "12 Ly Thuong Kiet, Hoan Kiem, Ha Noi", "0903000002", "hello@hanoidigital.local"),
            new OwnerTenantSeed("owner.danang@chainpos.local", "Owner@123", "Le Anh Tuan", "Da Nang Mobile Center", TenantStatuses.Suspended, "0403333333", "86 Nguyen Van Linh, Hai Chau, Da Nang", "0903000003", "support@danangmobile.local")
        };

        foreach (var seed in seeds)
        {
            var owner = await EnsureUserAsync(
                db,
                passwordHasher,
                seed.Email,
                seed.Password,
                seed.FullName,
                null,
                admin.Id,
                cancellationToken,
                seed.Phone);
            await EnsureUserRoleAsync(db, owner, AppRoles.Owner, cancellationToken);

            var tenant = await EnsureTenantAsync(
                db,
                owner,
                seed.TenantName,
                seed.Status,
                seed.TaxCode,
                seed.Address,
                seed.Phone,
                seed.TenantEmail,
                cancellationToken);
            await EnsureOwnerTenantAsync(db, owner, tenant, cancellationToken);
            await EnsureTenantSubscriptionAsync(db, tenant, plan, cancellationToken);
        }
    }

    private static async Task EnsureSeedAuditLogsAsync(
        StoreFlowDbContext db,
        AspNetUser admin,
        AspNetUser owner,
        Tenant tenant,
        IReadOnlyDictionary<string, Store> stores,
        IReadOnlyDictionary<string, AspNetUser> staff,
        IReadOnlyDictionary<string, Category> categories,
        IReadOnlyDictionary<string, Product> products,
        IReadOnlyDictionary<string, StoreProduct> storeProducts,
        CancellationToken cancellationToken)
    {
        await EnsureAuditLogAsync(db, "CreateTenant", nameof(Tenant), tenant.Id.ToString(), admin.Id, tenant.Id, null, $"Tenant={tenant.Name}", cancellationToken);

        foreach (var store in stores.Values)
        {
            await EnsureAuditLogAsync(db, "CreateStore", nameof(Store), store.Id.ToString(), owner.Id, tenant.Id, store.Id, $"Store={store.Name}; Code={store.Code}", cancellationToken);
        }

        foreach (var user in staff.Values)
        {
            await EnsureAuditLogAsync(db, "CreateStaff", nameof(AspNetUser), user.Id, owner.Id, tenant.Id, null, $"Staff={user.Email}", cancellationToken);
        }

        foreach (var category in categories.Values)
        {
            await EnsureAuditLogAsync(db, "CreateCategory", nameof(Category), category.Id.ToString(), owner.Id, tenant.Id, null, $"Category={category.Name}", cancellationToken);
        }

        foreach (var product in products.Values)
        {
            await EnsureAuditLogAsync(db, "CreateProduct", nameof(Product), product.Id.ToString(), owner.Id, tenant.Id, null, $"Product={product.Name}; SKU={product.Sku}", cancellationToken);
        }

        foreach (var storeProduct in storeProducts.Values)
        {
            await EnsureAuditLogAsync(db, "AssignStoreProduct", nameof(StoreProduct), storeProduct.Id.ToString(), owner.Id, tenant.Id, storeProduct.StoreId, $"ProductId={storeProduct.ProductId}; Available={storeProduct.IsAvailable}", cancellationToken);
        }
    }

    private static async Task EnsureAuditViewerDemoLogsAsync(
        StoreFlowDbContext db,
        AspNetUser admin,
        AspNetUser owner,
        Tenant tenant,
        IReadOnlyDictionary<string, Store> stores,
        IReadOnlyDictionary<string, AspNetUser> staff,
        IReadOnlyDictionary<string, Product> products,
        IReadOnlyDictionary<string, StoreProduct> storeProducts,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var primaryStore = stores.TryGetValue(DemoStoreCode, out var hcm01)
            ? hcm01
            : stores.Values.First();
        var secondaryStore = stores.TryGetValue("TZ-HCM-02", out var hcm02)
            ? hcm02
            : primaryStore;
        var primaryStaff = staff.TryGetValue(DefaultStaffEmail, out var staff01)
            ? staff01
            : staff.Values.First();
        var secondaryStaff = staff.TryGetValue("staff02@demo.local", out var staff02)
            ? staff02
            : primaryStaff;
        var macbook = products.TryGetValue("MBP14-M3P-18-512", out var mbp)
            ? mbp
            : products.Values.First();
        var mouse = products.TryGetValue("LOGI-MX3S-GR", out var mxMouse)
            ? mxMouse
            : macbook;
        var storeProduct = storeProducts.TryGetValue($"{DemoStoreCode}:LOGI-MX3S-GR", out var hcmMouse)
            ? hcmMouse
            : storeProducts.Values.First();

        var logs = new[]
        {
            new AuditDemoLog("Login", nameof(AspNetUser), admin.Id, admin.Id, null, null, null, "Email=admin@chainpos.local; Role=ADMIN", -35),
            new AuditDemoLog("Login", nameof(AspNetUser), owner.Id, owner.Id, tenant.Id, null, null, "Email=owner@demo.local; Role=OWNER", -32),
            new AuditDemoLog("CreateStore", nameof(Store), primaryStore.Id.ToString(), owner.Id, tenant.Id, primaryStore.Id, null, $"Store={primaryStore.Name}; Code={primaryStore.Code}", -29),
            new AuditDemoLog("UpdateProduct", nameof(Product), macbook.Id.ToString(), owner.Id, tenant.Id, null, "Price=52990000", $"Product={macbook.Name}; SKU={macbook.Sku}; Price=52490000", -26),
            new AuditDemoLog("AssignStoreProduct", nameof(StoreProduct), storeProduct.Id.ToString(), owner.Id, tenant.Id, primaryStore.Id, null, $"Store={primaryStore.Code}; ProductId={storeProduct.ProductId}; Available={storeProduct.IsAvailable}", -23),
            new AuditDemoLog("ImportStock", nameof(InventoryTransaction), "DEMO-AUDIT-IMPORT-001", secondaryStaff.Id, tenant.Id, primaryStore.Id, "Quantity=4", $"Product={mouse.Name}; Quantity=24; Reason=Supplier delivery", -20),
            new AuditDemoLog("AdjustStock", nameof(InventoryTransaction), "DEMO-AUDIT-ADJUST-001", owner.Id, tenant.Id, primaryStore.Id, "Quantity=24", $"Product={mouse.Name}; Quantity=22; Reason=Display unit count", -17),
            new AuditDemoLog("ExportStock", nameof(InventoryTransaction), "DEMO-AUDIT-EXPORT-001", primaryStaff.Id, tenant.Id, secondaryStore.Id, "Quantity=10", $"Product={mouse.Name}; Quantity=8; Reason=Inter-store transfer", -14),
            new AuditDemoLog("LockStaff", nameof(AspNetUser), secondaryStaff.Id, owner.Id, tenant.Id, null, "Status=Active", $"Staff={secondaryStaff.Email}; Status=Locked", -11),
            new AuditDemoLog("UnlockStaff", nameof(AspNetUser), secondaryStaff.Id, owner.Id, tenant.Id, null, "Status=Locked", $"Staff={secondaryStaff.Email}; Status=Active", -9),
            new AuditDemoLog("DisableStoreProduct", nameof(StoreProduct), storeProduct.Id.ToString(), owner.Id, tenant.Id, primaryStore.Id, "Available=True", $"Store={primaryStore.Code}; Product={mouse.Name}; Available=False", -7),
            new AuditDemoLog("EnableStoreProduct", nameof(StoreProduct), storeProduct.Id.ToString(), owner.Id, tenant.Id, primaryStore.Id, "Available=False", $"Store={primaryStore.Code}; Product={mouse.Name}; Available=True", -5),
            new AuditDemoLog("SuspendTenant", nameof(Tenant), tenant.Id.ToString(), admin.Id, tenant.Id, null, "Status=Active", $"Tenant={tenant.Name}; Status=Suspended", -4),
            new AuditDemoLog("ActivateTenant", nameof(Tenant), tenant.Id.ToString(), admin.Id, tenant.Id, null, "Status=Suspended", $"Tenant={tenant.Name}; Status=Active", -3),
            new AuditDemoLog("Logout", nameof(AspNetUser), owner.Id, owner.Id, tenant.Id, null, null, "User=owner@demo.local", -1)
        };

        foreach (var log in logs)
        {
            await EnsureAuditLogAsync(
                db,
                log.Action,
                log.EntityName,
                log.EntityId,
                log.UserId,
                log.TenantId,
                log.StoreId,
                log.NewValue,
                cancellationToken,
                createdAt: now.AddMinutes(log.MinutesFromNow),
                oldValue: log.OldValue);
        }
    }

    private static async Task EnsureAuditLogAsync(
        StoreFlowDbContext db,
        string action,
        string entityName,
        string entityId,
        string userId,
        Guid? tenantId,
        Guid? storeId,
        string newValue,
        CancellationToken cancellationToken,
        DateTime? createdAt = null,
        string? oldValue = null)
    {
        var exists = await db.AuditLogs.AnyAsync(
            x => x.Action == action && x.EntityName == entityName && x.EntityId == entityId,
            cancellationToken);
        if (exists)
        {
            return;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            UserId = userId,
            TenantId = tenantId,
            StoreId = storeId,
            OldValue = oldValue,
            NewValue = newValue,
            IpAddress = "seed",
            UserAgent = "DevelopmentDataSeeder",
            CreatedAt = createdAt ?? DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private sealed record StoreSeed(string Code, string Name, string Address, string Phone, string Status);

    private sealed record StaffSeed(string Email, string Password, string FullName, string Phone, IReadOnlyList<string> StoreCodes);

    private sealed record CategorySeed(string Name, string Description);

    private sealed record ProductSeed(
        string CategoryName,
        string Name,
        string Sku,
        string Barcode,
        string Description,
        decimal Price,
        decimal CostPrice,
        bool IsActive);

    private sealed record StoreProductSeed(string StoreCode, string Sku, decimal? SellingPrice, bool IsAvailable);

    private sealed record SystemPaymentSeed(
        decimal Amount,
        string Method,
        string Status,
        DateTime? PaidAt,
        string InvoiceUrl,
        DateTime CreatedAt);

    private sealed record AuditDemoLog(
        string Action,
        string EntityName,
        string EntityId,
        string UserId,
        Guid? TenantId,
        Guid? StoreId,
        string? OldValue,
        string NewValue,
        int MinutesFromNow);

    private sealed record InventorySeed(string StoreProductKey, decimal Quantity, decimal MinQuantity);

    private sealed record OrderItemSeed(string Sku, decimal Quantity);

    private sealed record ResolvedOrderItemSeed(
        Guid ProductId,
        string ProductName,
        string? Sku,
        decimal Quantity,
        decimal UnitPrice,
        decimal LineTotal);

    private sealed record OwnerTenantSeed(
        string Email,
        string Password,
        string FullName,
        string TenantName,
        string Status,
        string TaxCode,
        string Address,
        string Phone,
        string TenantEmail);
}
